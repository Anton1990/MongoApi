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
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] QueryRequest request) =>
        Ok(await _roleService.SearchAsync(request));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var role = await _roleService.GetByIdAsync(id)
            ?? throw new NotFoundException("Role", id);
        return Ok(role);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Create(Role role)
    {
        var created = await _roleService.CreateAsync(role);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _roleService.DeleteAsync(id);
        if (!deleted) throw new NotFoundException("Role", id);
        return NoContent();
    }
}
