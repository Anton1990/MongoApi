using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Authorization;
using MongoApi.Models;
using MongoApi.Models.Dtos;
using MongoApi.Services.Abstractions;

namespace MongoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _orgService;
    private readonly ICurrentUserService  _currentUser;

    public OrganizationsController(
        IOrganizationService orgService,
        ICurrentUserService currentUser)
    {
        _orgService  = orgService;
        _currentUser = currentUser;
    }

    /// <summary>Список организаций текущего пользователя.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] QueryRequest request)
    {
        var userId = _currentUser.GetUserId();
        return Ok(await _orgService.GetForUserAsync(userId, request));
    }

    /// <summary>Создать организацию. Создатель получает роль Admin.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrganizationRequest request)
    {
        var userId = _currentUser.GetUserId();

        var org = new Organization
        {
            Name        = request.Name,
            Description = request.Description
        };

        var created = await _orgService.CreateAsync(org, userId, request.AdminRoleId);
        return CreatedAtAction(nameof(GetById), new { orgId = created.Id }, created);
    }

    /// <summary>Детали организации — только для участников.</summary>
    [HttpGet("{orgId}")]
    [Authorize(Policy = Permissions.OrgMember)]
    public async Task<IActionResult> GetById(string orgId)
    {
        var org = await _orgService.GetByIdAsync(orgId);
        return Ok(org);
    }

    /// <summary>Список участников организации — только для участников.</summary>
    [HttpGet("{orgId}/members")]
    [Authorize(Policy = Permissions.OrgMember)]
    public async Task<IActionResult> GetMembers(string orgId)
    {
        var members = await _orgService.GetMembersAsync(orgId);
        return Ok(members);
    }

    /// <summary>Добавить участника — только Admin.</summary>
    [HttpPost("{orgId}/members")]
    [Authorize(Policy = Permissions.OrgAdmin)]
    public async Task<IActionResult> AddMember(string orgId, AddMemberRequest request)
    {
        await _orgService.AddMemberAsync(orgId, request.UserId, request.RoleId);
        return NoContent();
    }

    /// <summary>Удалить участника — только Admin.</summary>
    [HttpDelete("{orgId}/members/{userId}")]
    [Authorize(Policy = Permissions.OrgAdmin)]
    public async Task<IActionResult> RemoveMember(string orgId, string userId)
    {
        await _orgService.RemoveMemberAsync(orgId, userId);
        return NoContent();
    }
}
