using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Models;

namespace MongoApi.Services;

public class ProductService
{
    private readonly IMongoCollection<Product> _products;

    public ProductService(MongoDbContext context)
    {
        _products = context.Products;
    }

    public async Task<List<Product>> GetAllAsync() =>
        await _products.Find(_ => true).ToListAsync();

    public async Task<Product?> GetByIdAsync(string id) =>
        await _products.Find(p => p.Id == id).FirstOrDefaultAsync();

    public async Task<Product> CreateAsync(Product product)
    {
        await _products.InsertOneAsync(product);
        return product;
    }

    public async Task<bool> UpdateAsync(string id, Product updated)
    {
        updated.Id = id;
        var result = await _products.ReplaceOneAsync(p => p.Id == id, updated);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _products.DeleteOneAsync(p => p.Id == id);
        return result.DeletedCount > 0;
    }
}
