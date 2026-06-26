using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoApi.Models;
using MongoApi.Settings;

namespace MongoApi.Infrastructure.Authentication;

public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public TokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public (string AccessToken, string RefreshToken) Create(User user)
    {
        var accessToken  = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();
        return (accessToken, refreshToken);
    }

    public ClaimsPrincipal Validate(string token)
    {
        // MapInboundClaims = false → claim "sub" остаётся "sub", не переименовывается в ClaimTypes.NameIdentifier
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, GetValidationParameters(), out _);
        return principal;
    }

    private string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new("sub",               user.Id!),
            new("preferred_username", user.Username),
            new("email",             user.Email),
            new("name",              $"{user.FirstName} {user.LastName}")
        };

        var key         = GetKey();
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires     = DateTime.UtcNow.AddSeconds(_settings.TokenLifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(claims),
            Expires            = expires,
            Issuer             = _settings.Issuer,
            SigningCredentials  = credentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token   = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private TokenValidationParameters GetValidationParameters() => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = GetKey(),
        ValidateIssuer           = true,
        ValidIssuer              = _settings.Issuer,
        ValidateAudience         = false,
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.Zero
    };

    private SymmetricSecurityKey GetKey() =>
        new(Encoding.UTF8.GetBytes(_settings.SecretKey));
}
