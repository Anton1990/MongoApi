using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoApi.Infrastructure.Exceptions;
using MongoApi.Models;
using MongoApi.Models.Dtos;
using MongoApi.Services.Abstractions;

namespace MongoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryRequest request) =>
        Ok(await _userService.SearchAsync(request));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var user = await _userService.GetByIdAsync(id)
            ?? throw new NotFoundException("User", id);
        return Ok(user);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        var user = new User
        {
            Username  = request.Username,
            FirstName = request.FirstName,
            LastName  = request.LastName,
            Email     = request.Email
        };

        var created = await _userService.CreateAsync(user, request.Password);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
