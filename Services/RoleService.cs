using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Models;
using MongoApi.Services.Abstractions;

namespace MongoApi.Services;

public class RoleService : BaseMongoService<Role>, IRoleService
{
    public RoleService(MongoDbContext context)
        : base(context.Roles) { }

    protected override HashSet<string> AllowedFilterFields => new()
    {
        nameof(Role.Name)
    };

    public async Task<Role?> GetByNameAsync(string name) =>
        await _collection.Find(r => r.Name == name).FirstOrDefaultAsync();
}
