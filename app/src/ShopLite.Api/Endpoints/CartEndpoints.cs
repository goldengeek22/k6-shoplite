using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShopLite.Api.Auth;
using ShopLite.Api.Contracts;
using ShopLite.Api.Data;
using ShopLite.Api.Domain;

namespace ShopLite.Api.Endpoints;

public static class CartEndpoints
{
    private const int MaxQuantityPerLine = 100;

    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cart").RequireAuthorization();
        group.MapGet("/", GetCart);
        group.MapPost("/items", AddItem);
        group.MapDelete("/", ClearCart);
        return app;
    }

    private static async Task<IResult> GetCart(ClaimsPrincipal principal, ShopDbContext db, CancellationToken ct)
    {
        var userId = principal.GetUserId();

        var lines = await db.CartItems
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.AddedAt)
            .Select(c => new CartLineDto(
                c.ProductId,
                c.Product.Name,
                c.Product.Price,
                c.Quantity,
                c.Product.Price * c.Quantity))
            .ToListAsync(ct);

        return TypedResults.Ok(new CartDto(lines, lines.Sum(l => l.LineTotal)));
    }

    private static async Task<IResult> AddItem(
        AddCartItemRequest request,
        ClaimsPrincipal principal,
        ShopDbContext db,
        CancellationToken ct)
    {
        if (request.Quantity is < 1 or > MaxQuantityPerLine)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["quantity"] = [$"Quantity must be between 1 and {MaxQuantityPerLine}."]
            });
        }

        var userId = principal.GetUserId();

        var productExists = await db.Products.AnyAsync(p => p.Id == request.ProductId, ct);
        if (!productExists)
            return TypedResults.NotFound(new ErrorResponse($"Product {request.ProductId} not found."));

        var item = await db.CartItems
            .SingleOrDefaultAsync(c => c.UserId == userId && c.ProductId == request.ProductId, ct);

        if (item is null)
        {
            item = new CartItem { UserId = userId, ProductId = request.ProductId, Quantity = request.Quantity };
            db.CartItems.Add(item);
        }
        else
        {
            item.Quantity = Math.Min(item.Quantity + request.Quantity, MaxQuantityPerLine);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            // Two concurrent requests from the same user added the same product.
            return TypedResults.Conflict(new ErrorResponse("Cart was modified concurrently. Retry the request."));
        }

        return TypedResults.Ok(new CartItemResponse(item.ProductId, item.Quantity));
    }

    private static async Task<IResult> ClearCart(ClaimsPrincipal principal, ShopDbContext db, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        await db.CartItems.Where(c => c.UserId == userId).ExecuteDeleteAsync(ct);
        return TypedResults.NoContent();
    }
}
