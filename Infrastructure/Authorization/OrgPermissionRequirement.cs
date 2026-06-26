using Microsoft.AspNetCore.Authorization;

namespace MongoApi.Infrastructure.Authorization;

public class OrgPermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public OrgPermissionRequirement(string permission)
    {
        Permission = permission;
    }
}
