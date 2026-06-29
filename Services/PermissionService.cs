using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Authorization;
using MongoApi.Models;
using MongoApi.Services.Abstractions;

namespace MongoApi.Services;

public class PermissionService : IPermissionService
{
    private readonly IMongoCollection<UserResourceRole> _userResourceRoles;
    private readonly IMongoCollection<Product>          _products;

    public PermissionService(MongoDbContext context)
    {
        _userResourceRoles = context.UserResourceRoles;
        _products          = context.Products;
    }

    public async Task<bool> HasPermissionAsync(
        string userId,
        string resourceId,
        string resourceType,
        string requiredRole)
    {
        var entry = await _userResourceRoles
            .Find(x => x.UserId == userId
                    && x.ResourceId == resourceId
                    && x.ResourceType == resourceType)
            .FirstOrDefaultAsync();

        if (entry == null) return false;

        return RoleSatisfies(entry.RoleName, requiredRole);
    }

    /// <summary>
    /// Роутер специфических разрешений — каждое со своей логикой.
    /// resourceIds передаются в том порядке что указан в [AuthorizePermission(...)].
    /// </summary>
    public Task<bool> CheckPermissionAsync(string userId, string permission, string[] resourceIds) =>
        permission switch
        {
            Permissions.DeleteProduct => CanDeleteProductAsync(userId, resourceIds[0], resourceIds[1]),
            Permissions.RemoveMember  => CanRemoveMemberAsync(userId, resourceIds[0], resourceIds[1]),
            _                         => Task.FromResult(false)
        };

    // Удалить продукт: создатель продукта ИЛИ Admin организации-владельца
    private async Task<bool> CanDeleteProductAsync(string userId, string productId, string orgId)
    {
        var product = await _products
            .Find(p => p.Id == productId)
            .FirstOrDefaultAsync();

        if (product == null) return false;

        // Создатель может удалить свой продукт
        if (product.CreatedBy == userId) return true;

        // Продукт должен принадлежать именно этой организации
        if (product.OrganizationId != orgId) return false;

        // Admin этой организации может удалить продукт
        return await HasPermissionAsync(userId, orgId, ResourceType.Organization, Roles.Admin);
    }

    // Удалить пользователя из орга: Admin организации, но не последний Admin
    private async Task<bool> CanRemoveMemberAsync(string userId, string targetUserId, string orgId)
    {
        var isAdmin = await HasPermissionAsync(userId, orgId, ResourceType.Organization, Roles.Admin);
        if (!isAdmin) return false;

        // Нельзя удалить последнего Admin
        var adminCount = await _userResourceRoles.CountDocumentsAsync(x =>
            x.ResourceId   == orgId                    &&
            x.ResourceType == ResourceType.Organization &&
            x.RoleName     == Roles.Admin);

        return !(adminCount == 1 && targetUserId == userId);
    }

    private static bool RoleSatisfies(string actual, string required) => required switch
    {
        Roles.Admin  => actual == Roles.Admin,
        Roles.Member => actual is Roles.Admin or Roles.Member,
        Roles.Viewer => actual is Roles.Admin or Roles.Member or Roles.Viewer,
        _            => false
    };
}
