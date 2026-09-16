namespace ShopLite.Api.Domain;

public class User
{
    public long Id { get; set; }
    public required string Username { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}