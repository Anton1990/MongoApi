using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Authorization;
using MongoApi.Models;
using MongoApi.Services.Abstractions;

namespace MongoApi.Services;

public class PermissionService : IPermissionService
{
    private readonly IMongoCollection<UserResourceRole> _userResourceRoles;

    public PermissionService(MongoDbContext context)
    {
        _userResourceRoles = context.UserResourceRoles;
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

        return requiredRole switch
        {
            Roles.Admin  => entry.RoleName == Roles.Admin,
            Roles.Member => entry.RoleName is Roles.Admin or Roles.Member,
            Roles.Viewer => entry.RoleName is Roles.Admin or Roles.Member or Roles.Viewer,
            _            => false
        };
    }
}
