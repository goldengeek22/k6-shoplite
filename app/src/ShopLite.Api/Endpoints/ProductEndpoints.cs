using Microsoft.EntityFrameworkCore;
using ShopLite.Api.Chaos;
using ShopLite.Api.Contracts;
using ShopLite.Api.Data;
using ShopLite.Api.Domain;

namespace ShopLite.Api.Endpoints;

public static class ProductEndpoints
{
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/products");
        group.MapGet("/", Search);
        group.MapGet("/{id:long}", GetById);

        return app;
    }

    private static async Task<IResult> Search(
        ShopDbContext db,
        ChaosState chaos,
        CancellationToken cancellationToken,
        int page = 0,
        int size = 20,
        string? q = null,
        string? category = null
    )
    {
        if (page < 0 || size is < 1 or > MaxPageSize)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["paging"] = [$"page must be >= 0 and size must be between 1 and {MaxPageSize}."]
            });
        }

        IQueryable<Product> query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = chaos.Current.SlowQuery
            ? query.Where(p => EF.Functions.ILike(p.Description, $"%{q}%"))
            : query.Where(p => p.Name.StartsWith(q));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
        .OrderBy(p => p.Id)
        .Skip(page * size)
        .Take(size)
        .Select(p => new ProductDto(p.Id, p.Sku, p.Name, p.Description, p.Category, p.Price, p.Stock))
        .ToListAsync(cancellationToken);

        return TypedResults.Ok(new PagedResponse<ProductDto>(items, page, size, total));
    }

    private static async Task<IResult> GetById(
        long id,
        ShopDbContext db,
        CancellationToken cancellationToken
    )
    {
        var product = await db.Products
        .AsNoTracking()
        .Where(p => p.Id == id)
        .Select(p => new ProductDto(p.Id, p.Sku, p.Name, p.Description, p.Category, p.Price, p.Stock))
        .SingleOrDefaultAsync();

        return product is null
        ? TypedResults.NotFound(new ErrorResponse($"Product {id} not found"))
        : TypedResults.Ok(product);
    }
}
