using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Exceptions;
using MongoApi.Models.Dtos;
using MongoApi.Services.Abstractions;

namespace MongoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService        _authService;
    private readonly IUserService        _userService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(
        IAuthService authService,
        IUserService userService,
        ICurrentUserService currentUser)
    {
        _authService = authService;
        _userService = userService;
        _currentUser = currentUser;
    }

    /// <summary>Вход по username + password. Возвращает JWT access token и refresh token.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var response = await _authService.LoginAsync(request.Username, request.Password);
        return Ok(response);
    }

    /// <summary>Профиль текущего авторизованного пользователя.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = _currentUser.GetUserId();

        var user = await _userService.GetByIdAsync(userId)
            ?? throw new NotFoundException("User", userId);

        return Ok(new UserProfileResponse
        {
            Id        = user.Id!,
            Username  = user.Username,
            FirstName = user.FirstName,
            LastName  = user.LastName,
            Email     = user.Email,
            IsActive  = user.IsActive,
            CreatedAt = user.CreatedAt
        });
    }
}
