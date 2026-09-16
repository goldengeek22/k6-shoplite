using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ShopLite.Api.Contracts;
using ShopLite.Api.Domain;

namespace ShopLite.Api.Auth;

public class TokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _credentials;
    private readonly JsonWebTokenHandler _handler = new();

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public LoginResponse CreateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt,
            SigningCredentials = _credentials,
            Subject = new ClaimsIdentity(new[]
            {
               new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
               new Claim(JwtRegisteredClaimNames.UniqueName, user.Username) 
            }),
        };

        return new LoginResponse(_handler.CreateToken(descriptor), expiresAt);
    }
}
