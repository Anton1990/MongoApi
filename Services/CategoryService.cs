using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Models;

namespace MongoApi.Services;

public class CategoryService
{
    private readonly IMongoCollection<Category> _categories;

    public CategoryService(MongoDbContext context)
    {
        _categories = context.Categories;
    }

    public async Task<List<Category>> GetAllAsync() =>
        await _categories.Find(_ => true).ToListAsync();

    public async Task<Category?> GetByIdAsync(string id) =>
        await _categories.Find(c => c.Id == id).FirstOrDefaultAsync();

    public async Task<Category> CreateAsync(Category category)
    {
        await _categories.InsertOneAsync(category);
        return category;
    }

    public async Task<bool> UpdateAsync(string id, Category updated)
    {
        updated.Id = id;
        var result = await _categories.ReplaceOneAsync(c => c.Id == id, updated);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _categories.DeleteOneAsync(c => c.Id == id);
        return result.DeletedCount > 0;
    }
}
