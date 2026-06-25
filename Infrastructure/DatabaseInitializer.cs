using MongoDB.Bson;
using MongoDB.Driver;
using MongoApi.Models;

namespace MongoApi.Infrastructure;

public class DatabaseInitializer
{
    private readonly IMongoDatabase _db;
    private readonly IHostEnvironment _env;

    public DatabaseInitializer(MongoDbContext context, IHostEnvironment env)
    {
        _db = context.Database;
        _env = env;
    }

    public async Task InitializeAsync()
    {
        await CreateProductIndexesAsync();
        await CreateCustomerIndexesAsync();
        await MigrateProductStatusAsync();
        if (_env.IsDevelopment())
            await EnableProfilerAsync();
    }

    private async Task EnableProfilerAsync()
    {
        // Профилируем все запросы (slowms: 0) — только в Development
        await _db.RunCommandAsync<BsonDocument>(
            new BsonDocument { { "profile", 1 }, { "slowms", 0 } });
    }

    private async Task MigrateProductStatusAsync()
    {
        var collection = _db.GetCollection<Product>("products");

        var result = await collection.UpdateManyAsync(
            Builders<Product>.Filter.Exists(p => p.Status, false),
            Builders<Product>.Update.Set(p => p.Status, ProductStatus.Active));

        if (result.ModifiedCount > 0)
            Console.WriteLine($"[Migration] Set status=Active on {result.ModifiedCount} products.");
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
            ),
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys.Ascending("manufacturer.country"),
                new CreateIndexOptions { Name = "idx_product_manufacturer_country" }
            ),
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys.Descending(p => p.CreatedAt),
                new CreateIndexOptions { Name = "idx_product_createdAt" }
            ),
            // Составной индекс для типичного запроса: filter(status,isAvailable,price) + sort(price,name)
            // ESR: Equality(status,isAvailable) → Sort+Range(price) → Sort(name)
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys
                    .Ascending(p => p.Status)
                    .Ascending(p => p.IsAvailable)
                    .Descending(p => p.Price)
                    .Ascending(p => p.Name),
                new CreateIndexOptions { Name = "idx_product_status_available_price_name" }
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
