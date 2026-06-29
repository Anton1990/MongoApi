using HotChocolate.Authorization;
using HotChocolate.Data;
using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Authorization;
using MongoApi.Models;
using MongoApi.Services.Abstractions;

namespace MongoApi.GraphQL;

public class OrganizationQuery
{
    /// <summary>
    /// Организации текущего пользователя.
    /// HotChocolate применяет filtering/sorting/paging поверх IQueryable.
    ///
    /// query {
    ///   myOrganizations(
    ///     where: { name: { contains: "Siem" } }
    ///     order: { name: ASC }
    ///     first: 10
    ///   ) {
    ///     nodes { id name description }
    ///     pageInfo { hasNextPage endCursor }
    ///     totalCount
    ///   }
    /// }
    /// </summary>
    [Authorize]
    [UseOffsetPaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Organization> GetMyOrganizations(
        [Service] ICurrentUserService                currentUser,
        [Service] IMongoCollection<UserResourceRole> userResourceRoles,
        [Service] IOrganizationService               orgService)
    {
        var userId = currentUser.GetUserId();

        // Получаем orgIds синхронно — IQueryable требует синхронный источник данных
        var orgIds = userResourceRoles
            .Find(m => m.UserId == userId && m.ResourceType == ResourceType.Organization)
            .ToList()
            .Select(m => m.ResourceId)
            .ToList();

        return orgService.GetQueryable().Where(o => orgIds.Contains(o.Id!));
    }

    /// <summary>
    /// Одна организация по ID. Требует членства (Roles.Member).
    ///
    /// query { organization(id: "abc123") { id name } }
    /// </summary>
    [Authorize]
    public async Task<Organization?> GetOrganization(
        string id,
        [Service] ICurrentUserService  currentUser,
        [Service] IPermissionService   permissionService,
        [Service] IOrganizationService orgService)
    {
        var userId = currentUser.GetUserId();

        var allowed = await permissionService.HasPermissionAsync(
            userId, id, ResourceType.Organization, Roles.Member);

        if (!allowed)
            throw new UnauthorizedAccessException("You are not a member of this organization.");

        return await orgService.GetByIdAsync(id);
    }
}
