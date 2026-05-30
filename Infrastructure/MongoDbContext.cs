using MongoDB.Driver;
using MongoApi.Models;
using MongoApi.Settings;
using Microsoft.Extensions.Options;

namespace MongoApi.Infrastructure;

/// <summary>
/// Аналог DbContext — один на всё приложение (Singleton).
/// MongoClient внутри держит connection pool.
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<Product> Products =>
        _database.GetCollection<Product>("products");

    public IMongoCollection<Customer> Customers =>
        _database.GetCollection<Customer>("customers");

    public IMongoCollection<Customer> Customers2 =>
        _database.GetCollection<Customer>("customers2");

    public IMongoDatabase Database => _database;
}
