using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Authorization;
using MongoApi.Infrastructure.Exceptions;
using MongoApi.Models;
using MongoApi.Models.Dtos;
using MongoApi.Services.Abstractions;

namespace MongoApi.Services;

public class OrganizationService : BaseMongoService<Organization>, IOrganizationService
{
    private readonly IMongoCollection<UserResourceRole> _userResourceRoles;
    private readonly IMongoCollection<Role>             _roles;

    public OrganizationService(MongoDbContext context)
        : base(context.Organizations)
    {
        _userResourceRoles = context.UserResourceRoles;
        _roles             = context.Roles;
    }

    protected override HashSet<string> AllowedFilterFields => new()
    {
        nameof(Organization.Name),
        nameof(Organization.Description),
        nameof(Organization.CreatedAt)
    };

    public async Task<PagedResult<Organization>> GetForUserAsync(string userId, QueryRequest request)
    {
        var memberships = await _userResourceRoles
            .Find(m => m.UserId == userId && m.ResourceType == ResourceType.Organization)
            .ToListAsync();

        var orgIds = memberships.Select(m => m.ResourceId).ToList();

        return await SearchAsync(request, o => orgIds.Contains(o.Id!));
    }

#pragma warning disable CS8613 // Nullability narrowing is intentional: throws instead of returning null
    public new async Task<Organization> GetByIdAsync(string orgId)
    {
        return await base.GetByIdAsync(orgId)
            ?? throw new NotFoundException("Organization", orgId);
    }
#pragma warning restore CS8613

    public async Task<Organization> CreateAsync(Organization org, string creatorUserId)
    {
        await _collection.InsertOneAsync(org);

        await _userResourceRoles.InsertOneAsync(new UserResourceRole
        {
            UserId       = creatorUserId,
            ResourceId   = org.Id!,
            ResourceType = ResourceType.Organization,
            RoleName     = Roles.Admin
        });

        return org;
    }

    public async Task<List<UserResourceRole>> GetMembersAsync(string orgId)
    {
        return await _userResourceRoles
            .Find(m => m.ResourceId == orgId && m.ResourceType == ResourceType.Organization)
            .ToListAsync();
    }

    public async Task AddMemberAsync(string orgId, string userId, string roleId)
    {
        var exists = await _userResourceRoles
            .Find(m => m.ResourceId == orgId
                    && m.ResourceType == ResourceType.Organization
                    && m.UserId == userId)
            .AnyAsync();

        if (exists)
            throw new ConflictException($"User '{userId}' is already a member of this organization.");

        var roleName = await GetRoleNameAsync(roleId);

        await _userResourceRoles.InsertOneAsync(new UserResourceRole
        {
            UserId       = userId,
            ResourceId   = orgId,
            ResourceType = ResourceType.Organization,
            RoleName     = roleName
        });
    }

    public async Task RemoveMemberAsync(string orgId, string userId)
    {
        var result = await _userResourceRoles
            .DeleteOneAsync(m => m.ResourceId == orgId
                             && m.ResourceType == ResourceType.Organization
                             && m.UserId == userId);

        if (result.DeletedCount == 0)
            throw new NotFoundException($"User '{userId}' is not a member of this organization.");
    }

    private async Task<string> GetRoleNameAsync(string roleId)
    {
        var role = await _roles.Find(r => r.Id == roleId).FirstOrDefaultAsync()
            ?? throw new NotFoundException("Role", roleId);
        return role.Name;
    }
}
