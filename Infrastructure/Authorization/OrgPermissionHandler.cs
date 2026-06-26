using Microsoft.AspNetCore.Authorization;
using MongoDB.Driver;
using MongoApi.Models;

namespace MongoApi.Infrastructure.Authorization;

/// <summary>
/// Проверяет что текущий пользователь имеет нужную роль в организации из route {orgId}.
/// Работает как в EnterAR: читает orgId из route → ищет UserOrganizationRole → проверяет роль.
/// </summary>
public class OrgPermissionHandler : AuthorizationHandler<OrgPermissionRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMongoCollection<UserOrganizationRole> _userOrgRoles;
    private readonly IMongoCollection<Role> _roles;

    public OrgPermissionHandler(
        IHttpContextAccessor httpContextAccessor,
        MongoDbContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _userOrgRoles = context.UserOrganizationRoles;
        _roles = context.Roles;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrgPermissionRequirement requirement)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
            return; // → 401

        var httpContext = _httpContextAccessor.HttpContext;
        var orgId = httpContext?.Request.RouteValues["orgId"]?.ToString();

        // Если orgId нет в route — endpoint не привязан к организации, пропускаем
        if (string.IsNullOrEmpty(orgId))
        {
            context.Succeed(requirement);
            return;
        }

        var membership = await _userOrgRoles
            .Find(m => m.UserId == userId && m.OrganizationId == orgId)
            .FirstOrDefaultAsync();

        if (membership == null)
            return; // → 403

        var role = await _roles
            .Find(r => r.Id == membership.RoleId)
            .FirstOrDefaultAsync();

        if (role == null)
            return; // → 403

        var allowed = requirement.Permission switch
        {
            Permissions.OrgMember => true,                    // любой член организации
            Permissions.OrgAdmin  => role.Name == "Admin",    // только Admin
            _                     => false
        };

        if (allowed)
            context.Succeed(requirement);
    }
}
