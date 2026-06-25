using MongoApi.Infrastructure;
using MongoApi.Models;

namespace MongoApi.Services.Abstractions;

public interface ICategoryService : IBaseMongoService<Category>
{
    Task<bool> UpdateAsync(string id, Category updated);
}
