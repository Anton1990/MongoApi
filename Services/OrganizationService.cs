using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Exceptions;
using MongoApi.Models;
using MongoApi.Models.Dtos;
using MongoApi.Services.Abstractions;

namespace MongoApi.Services;

public class OrganizationService : IOrganizationService
{
    private readonly IMongoCollection<Organization> _orgs;
    private readonly IMongoCollection<UserOrganizationRole> _userOrgRoles;

    public OrganizationService(MongoDbContext context)
    {
        _orgs = context.Organizations;
        _userOrgRoles = context.UserOrganizationRoles;
    }

    /// <summary>Возвращает только организации где userId является участником.</summary>
    public async Task<PagedResult<Organization>> GetForUserAsync(string userId, QueryRequest request)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var page = Math.Max(1, request.Page);

        // Находим все orgId где участвует пользователь
        var memberships = await _userOrgRoles
            .Find(m => m.UserId == userId)
            .ToListAsync();

        var orgIds = memberships.Select(m => m.OrganizationId).ToList();

        var filter = Builders<Organization>.Filter.In(o => o.Id, orgIds);
        var total = await _orgs.CountDocumentsAsync(filter);
        var items = await _orgs.Find(filter)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        return new PagedResult<Organization>(items, page, pageSize, (int)total);
    }

    public async Task<Organization> GetByIdAsync(string orgId)
    {
        return await _orgs.Find(o => o.Id == orgId).FirstOrDefaultAsync()
            ?? throw new NotFoundException("Organization", orgId);
    }

    /// <summary>Создаёт организацию и назначает создателя Admin'ом.</summary>
    public async Task<Organization> CreateAsync(Organization org, string creatorUserId, string adminRoleId)
    {
        await _orgs.InsertOneAsync(org);

        await _userOrgRoles.InsertOneAsync(new UserOrganizationRole
        {
            UserId = creatorUserId,
            OrganizationId = org.Id!,
            RoleId = adminRoleId
        });

        return org;
    }

    public async Task<List<UserOrganizationRole>> GetMembersAsync(string orgId)
    {
        return await _userOrgRoles
            .Find(m => m.OrganizationId == orgId)
            .ToListAsync();
    }

    public async Task AddMemberAsync(string orgId, string userId, string roleId)
    {
        var exists = await _userOrgRoles
            .Find(m => m.OrganizationId == orgId && m.UserId == userId)
            .AnyAsync();

        if (exists)
            throw new ConflictException($"User '{userId}' is already a member of this organization.");

        await _userOrgRoles.InsertOneAsync(new UserOrganizationRole
        {
            UserId = userId,
            OrganizationId = orgId,
            RoleId = roleId
        });
    }

    public async Task RemoveMemberAsync(string orgId, string userId)
    {
        var result = await _userOrgRoles
            .DeleteOneAsync(m => m.OrganizationId == orgId && m.UserId == userId);

        if (result.DeletedCount == 0)
            throw new NotFoundException($"User '{userId}' is not a member of this organization.");
    }
}
