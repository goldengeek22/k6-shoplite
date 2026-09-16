using Microsoft.EntityFrameworkCore;
using ShopLite.Api.Domain;

namespace ShopLite.Api.Data;

public class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(u => u.Username).HasMaxLength(50);
            e.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Sku).HasMaxLength(32);
            e.HasIndex(p => p.Sku).IsUnique();

            e.Property(p => p.Name).HasMaxLength(200);
            // text_pattern_ops lets PostgreSQL use this index for prefix searches (LIKE 'abc%')
            // regardless of the database collation.
            e.HasIndex(p => p.Name).HasOperators("text_pattern_ops");

            e.Property(p => p.Category).HasMaxLength(50);
            e.HasIndex(p => p.Category);

            e.Property(p => p.Price).HasPrecision(10, 2);
            // Description is deliberately NOT indexed: the slowQuery chaos mode scans it.
        });

        modelBuilder.Entity<CartItem>(e =>
        {
            e.HasIndex(c => new { c.UserId, c.ProductId }).IsUnique();
            e.HasOne<User>().WithMany().HasForeignKey(c => c.UserId);
            e.HasOne(c => c.Product).WithMany().HasForeignKey(c => c.ProductId);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.UserId);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(o => o.Total).HasPrecision(12, 2);
            e.HasOne<User>().WithMany().HasForeignKey(o => o.UserId);
            e.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.Property(i => i.ProductName).HasMaxLength(200);
            e.Property(i => i.UnitPrice).HasPrecision(10, 2);
        });
    }
}
