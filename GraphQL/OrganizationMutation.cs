using HotChocolate.Authorization;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Authorization;
using MongoApi.Models;
using MongoApi.Services.Abstractions;

namespace MongoApi.GraphQL;

[ExtendObjectType(OperationTypeNames.Mutation)]
public class OrganizationMutation
{
    /// <summary>
    /// Создаёт организацию. Вызывающий пользователь автоматически становится Admin.
    ///
    /// mutation {
    ///   createOrganization(input: { name: "Acme", description: "..." }) {
    ///     id name
    ///   }
    /// }
    /// </summary>
    [Authorize]
    public async Task<Organization> CreateOrganization(
        CreateOrganizationInput input,
        [Service] ICurrentUserService  currentUser,
        [Service] IOrganizationService orgService)
    {
        var userId = currentUser.GetUserId();

        var org = new Organization
        {
            Name        = input.Name,
            Description = input.Description
        };

        return await orgService.CreateAsync(org, userId);
    }

    /// <summary>
    /// Добавляет участника в организацию (требует Admin).
    ///
    /// mutation {
    ///   addOrganizationMember(orgId: "...", userId: "...", roleId: "...")
    /// }
    /// </summary>
    [Authorize]
    public async Task<bool> AddOrganizationMember(
        string orgId,
        string userId,
        string roleId,
        [Service] ICurrentUserService  currentUser,
        [Service] IPermissionService   permissionService,
        [Service] IOrganizationService orgService)
    {
        var callerId = currentUser.GetUserId();

        var isAdmin = await permissionService.HasPermissionAsync(
            callerId, orgId, ResourceType.Organization, Roles.Admin);

        if (!isAdmin)
            throw new UnauthorizedAccessException("Only org admins can add members.");

        await orgService.AddMemberAsync(orgId, userId, roleId);
        return true;
    }
}
