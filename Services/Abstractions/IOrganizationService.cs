using MongoApi.Infrastructure;
using MongoApi.Models;
using MongoApi.Models.Dtos;

namespace MongoApi.Services.Abstractions;

public interface IOrganizationService : IBaseMongoService<Organization>
{
    Task<PagedResult<Organization>> GetForUserAsync(string userId, QueryRequest request);
    new Task<Organization> GetByIdAsync(string orgId);   // переопределяем: бросает 404 вместо null
    Task<Organization> CreateAsync(Organization org, string creatorUserId);
    Task<List<UserResourceRole>> GetMembersAsync(string orgId);
    Task AddMemberAsync(string orgId, string userId, string roleId);
    Task RemoveMemberAsync(string orgId, string userId);
}
