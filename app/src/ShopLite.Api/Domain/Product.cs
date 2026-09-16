namespace ShopLite.Api.Domain;

public class Product
{
    public long Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Category { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }    
}
