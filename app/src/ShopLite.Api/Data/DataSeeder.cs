using Microsoft.EntityFrameworkCore;
using ShopLite.Api.Domain;
namespace ShopLite.Api.Data;

public static class DataSeeder {

    private static readonly string[] Adjectives = ["Classic", "Blue", "Red", "Green", "Black", "White", "Vintage", "Premium", "Eco", "Urban", "Sport", "Cozy"];

    private static readonly string[] Materials = ["Cotton", "Leather", "Wool", "Denim", "Canvas", "Linen", "Steel", "Bamboo"];

    private static readonly (string Category, string[] Nouns)[] Catalog = [
      ("apparel", new[] { "Shirt", "Jacket", "Hoodie", "Jeans", "Scarf" }),
      ("footwear", new[] { "Sneakers", "Boots", "Sandals", "Loafers" }),
      ("accessories", new[] { "Backpack", "Wallet", "Belt", "Watch", "Cap" }),
      ("home", new[] { "Mug", "Blanket", "Lamp", "Cushion", "Vase" })
    ];

    public static async Task SeedAsync(ShopDbContext db, int productCount, ILogger logger, CancellationToken ct = default)
    {
         if (await db.Products.AnyAsync(ct))
        {
            logger.LogInformation("Products already seeded, skipping.");
            return;
        }

         logger.LogInformation("Seeding {Count} products...", productCount);

        var rng = new Random(42); // fixed seed: same catalog every time, reproducible tests
        const int batchSize = 2_000;

        for (var start = 0; start < productCount; start += batchSize)
        {
            var count = Math.Min(batchSize, productCount - start);
            var batch = new List<Product>(count);

            for (var i = start; i < start + count; i++)
            {
                var (category, nouns) = Catalog[rng.Next(Catalog.Length)];
                var name = $"{Adjectives[rng.Next(Adjectives.Length)]} " +
                           $"{Materials[rng.Next(Materials.Length)]} " +
                           $"{nouns[rng.Next(nouns.Length)]}";

                batch.Add(new Product
                {
                    Sku = $"SKU-{i + 1:D6}",
                    Name = name,
                    Description = $"A {name.ToLowerInvariant()} from the ShopLite {category} collection. Item #{i + 1}.",
                    Category = category,
                    Price = Math.Round((decimal)(rng.NextDouble() * 195 + 5), 2),
                    Stock = 1_000_000, // effectively unlimited so load tests don't fail on stock
                });
            }

            db.Products.AddRange(batch);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear(); // keep memory flat across batches
        }

        logger.LogInformation("Seeding complete.");
    }
}
