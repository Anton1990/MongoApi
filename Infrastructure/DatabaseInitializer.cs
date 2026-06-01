using MongoDB.Driver;
using MongoApi.Models;

namespace MongoApi.Infrastructure;

public class DatabaseInitializer
{
    private readonly IMongoDatabase _db;

    public DatabaseInitializer(MongoDbContext context)
    {
        _db = context.Database;
    }

    public async Task InitializeAsync()
    {
        await CreateProductIndexesAsync();
        await CreateCustomerIndexesAsync();
    }

    private async Task CreateProductIndexesAsync()
    {
        var collection = _db.GetCollection<Product>("products");

        var indexes = new[]
        {
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys.Ascending(p => p.CategoryId),
                new CreateIndexOptions { Name = "idx_product_category" }
            ),
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys.Ascending(p => p.IsAvailable),
                new CreateIndexOptions { Name = "idx_product_available" }
            )
        };

        await collection.Indexes.CreateManyAsync(indexes);
    }

    private async Task CreateCustomerIndexesAsync()
    {
        var collection = _db.GetCollection<Customer>("customers");

        var indexes = new[]
        {
            new CreateIndexModel<Customer>(
                Builders<Customer>.IndexKeys.Ascending(c => c.Email),
                new CreateIndexOptions { Unique = true, Name = "idx_customer_email_unique" }
            ),
            new CreateIndexModel<Customer>(
                Builders<Customer>.IndexKeys.Ascending(c => c.RegisteredAt),
                new CreateIndexOptions { Name = "idx_customer_registered" }
            )
        };

        await collection.Indexes.CreateManyAsync(indexes);
    }
}
