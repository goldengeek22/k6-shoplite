namespace ShopLite.Api.Domain;

public class CartItem
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
