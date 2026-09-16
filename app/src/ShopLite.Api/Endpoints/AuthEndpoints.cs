using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShopLite.Api.Auth;
using ShopLite.Api.Contracts;
using ShopLite.Api.Data;
using ShopLite.Api.Domain;

namespace ShopLite.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");
        app.MapPost("/register", Register);
        app.MapPost("/login", Login);
        return app;
    }    

    private static async Task<IResult> Register(RegisterRequest request, ShopDbContext db, IPasswordHasher<User> hasher, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var username = request.Username?.Trim();

        if (string.IsNullOrEmpty(username) || username.Length is < 3 or > 50)
        {
            errors["username"] = ["Username must be between 3 and 50 characters"];
        }
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 6)
            errors["password"] = ["Password must be at least 6 characters"];

        if (errors.Count > 0)
            return TypedResults.ValidationProblem(errors);

        var user = new User { Username = username! };
        user.PasswordHash = hasher.HashPassword(user, request.Password!);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }catch(DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            return TypedResults.Conflict(new ErrorResponse("Username is already taken."));
        }

        return TypedResults.Created($"/users/{user.Id}", new UserCreatedResponse(user.Id, user.Username));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        ShopDbContext db,
        IPasswordHasher<User> hasher,
        TokenService tokenService,
        CancellationToken cancellationToken)
    {
         var username = request.Username?.Trim();
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(request.Password))
            return TypedResults.Unauthorized();

        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null)
            return TypedResults.Unauthorized();

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return TypedResults.Unauthorized();

        return TypedResults.Ok(tokenService.CreateToken(user));
    }
}
