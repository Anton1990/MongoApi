using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MongoApi.Infrastructure.Authentication;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace MongoApi.Infrastructure;

/// <summary>
/// Читает Authorization: Bearer {jwt} → валидирует через TokenService → создаёт ClaimsPrincipal.
/// </summary>
public class HeaderAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ITokenService _tokenService;

    public HeaderAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ITokenService tokenService)
        : base(options, logger, encoder)
    {
        _tokenService = tokenService;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = authHeader["Bearer ".Length..].Trim();

        try
        {
            var principal = _tokenService.Validate(token);

            // Явно создаём ClaimsIdentity с Scheme.Name → IsAuthenticated = true
            // (JwtSecurityTokenHandler возвращает ClaimsIdentity с AuthenticationType = null)
            var identity  = new ClaimsIdentity(principal.Claims, Scheme.Name);
            var ticket    = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired token."));
        }
    }
}
