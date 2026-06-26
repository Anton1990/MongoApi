using Microsoft.AspNetCore.Identity;
using MongoApi.Infrastructure.Authentication;
using MongoApi.Infrastructure.Exceptions;
using MongoApi.Models.Dtos;
using MongoApi.Services.Abstractions;

namespace MongoApi.Services;

public class AuthService : IAuthService
{
    private readonly IUserService  _userService;
    private readonly ITokenService _tokenService;

    public AuthService(IUserService userService, ITokenService tokenService)
    {
        _userService  = userService;
        _tokenService = tokenService;
    }

    public async Task<TokenResponse> LoginAsync(string username, string password)
    {
        var user = await _userService.GetByUsernameAsync(username)
            ?? throw new UnauthorizedException("Invalid username or password.");

        if (!user.IsActive)
            throw new UnauthorizedException("Account is disabled.");

        var hasher = new PasswordHasher<string>();
        var result = hasher.VerifyHashedPassword(user.Email, user.PasswordHash, password);

        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedException("Invalid username or password.");

        var (accessToken, refreshToken) = _tokenService.Create(user);

        return new TokenResponse
        {
            Token        = accessToken,
            RefreshToken = refreshToken,
            UserId       = user.Id!
        };
    }
}
