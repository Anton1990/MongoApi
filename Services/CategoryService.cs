using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Models;
using MongoApi.Services.Abstractions;

namespace MongoApi.Services;

public class CategoryService : BaseMongoService<Category>, ICategoryService
{
    public CategoryService(MongoDbContext context)
        : base(context.Categories) { }

    protected override HashSet<string> AllowedFilterFields => new()
    {
        nameof(Category.Name),
        nameof(Category.Slug)
    };

    public async Task<bool> UpdateAsync(string id, Category updated)
    {
        updated.Id = id;
        var result = await _collection.ReplaceOneAsync(c => c.Id == id, updated);
        return result.ModifiedCount > 0;
    }
}
