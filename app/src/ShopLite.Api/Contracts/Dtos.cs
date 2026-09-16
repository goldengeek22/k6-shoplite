namespace ShopLite.Api.Contracts;

// Auth
public record RegisterRequest(string? Username, string? Password);
public record LoginRequest(string? Username, string? Password);
public record LoginResponse(string Token, DateTime ExpiresAt);
public record UserCreatedResponse(long Id, string Username);

// Products
public record ProductDto(long Id, string Sku, string Name, string Description, string Category, decimal Price, int Stock);
public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int Size, int Total);

// Cart
public record AddCartItemRequest(long ProductId, int Quantity);
public record CartItemResponse(long ProductId, int Quantity);
public record CartLineDto(long ProductId, string Name, decimal UnitPrice, int Quantity, decimal LineTotal);
public record CartDto(IReadOnlyList<CartLineDto> Items, decimal Total);

// Orders
public record OrderCreatedResponse(long OrderId, decimal Total);
public record OrderItemDto(long ProductId, string ProductName, decimal UnitPrice, int Quantity);
public record OrderDto(long Id, string Status, DateTime CreatedAt, decimal Total, IReadOnlyList<OrderItemDto> Items);

// Errors
public record ErrorResponse(string Error);
