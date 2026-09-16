using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShopLite.Api.Auth;
using ShopLite.Api.Contracts;
using ShopLite.Api.Data;
using ShopLite.Api.Domain;

namespace ShopLite.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/orders").RequireAuthorization();
        group.MapPost("/", CreateOrder);
        group.MapGet("/{id:long}", GetOrder);
        return app;
    }

    private static async Task<IResult> CreateOrder(ClaimsPrincipal principal, ShopDbContext db, CancellationToken ct)
    {
        var userId = principal.GetUserId();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Ordering by ProductId means concurrent orders always lock product rows in the same
        // order, which prevents deadlocks between two checkouts containing the same products.
        var cart = await db.CartItems
            .Where(c => c.UserId == userId)
            .Include(c => c.Product)
            .OrderBy(c => c.ProductId)
            .ToListAsync(ct);

        if (cart.Count == 0)
            return TypedResults.BadRequest(new ErrorResponse("Cart is empty."));

        foreach (var line in cart)
        {
            // Atomic conditional decrement: no read-modify-write race on stock.
            var updatedRows = await db.Products
                .Where(p => p.Id == line.ProductId && p.Stock >= line.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - line.Quantity), ct);

            if (updatedRows == 0)
            {
                await transaction.RollbackAsync(ct);
                return TypedResults.Conflict(new ErrorResponse($"Insufficient stock for product {line.ProductId}."));
            }
        }

        var order = new Order
        {
            UserId = userId,
            Items = cart.Select(c => new OrderItem
            {
                ProductId = c.ProductId,
                ProductName = c.Product.Name,
                UnitPrice = c.Product.Price,
                Quantity = c.Quantity,
            }).ToList(),
        };
        order.Total = order.Items.Sum(i => i.UnitPrice * i.Quantity);

        db.Orders.Add(order);
        db.CartItems.RemoveRange(cart);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return TypedResults.Created($"/orders/{order.Id}", new OrderCreatedResponse(order.Id, order.Total));
    }

    private static async Task<IResult> GetOrder(long id, ClaimsPrincipal principal, ShopDbContext db, CancellationToken ct)
    {
        var userId = principal.GetUserId();

        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == id, ct);

        if (order is null)
            return TypedResults.NotFound(new ErrorResponse($"Order {id} not found."));

        if (order.UserId != userId)
            return TypedResults.Forbid();

        return TypedResults.Ok(new OrderDto(
            order.Id,
            order.Status.ToString(),
            order.CreatedAt,
            order.Total,
            order.Items
                .Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity))
                .ToList()));
    }
}
