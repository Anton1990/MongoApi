using MongoApi.Infrastructure;
using MongoApi.Models;

namespace MongoApi.Services.Abstractions;

public interface IRoleService : IBaseMongoService<Role>
{
    Task<Role?> GetByNameAsync(string name);
}
