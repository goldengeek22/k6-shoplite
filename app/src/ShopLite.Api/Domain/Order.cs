namespace ShopLite.Api.Domain;

public enum OrderStatus
{
    Placed,
    Paid,
    Shipped,
    Cancelled
}

public class Order
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Placed;
    public decimal Total { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public long ProductId { get; set; }
    public required string ProductName { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
