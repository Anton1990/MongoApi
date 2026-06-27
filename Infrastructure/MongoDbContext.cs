using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoApi.Models;
using MongoApi.Settings;

namespace MongoApi.Infrastructure;

/// <summary>
/// Аналог DbContext — один на всё приложение (Singleton).
/// MongoClient внутри держит connection pool.
/// </summary>
public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public IMongoClient Client { get; }

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        Client = new MongoClient(settings.Value.ConnectionString);
        _database = Client.GetDatabase(settings.Value.DatabaseName);
    }

    public IMongoCollection<Product> Products =>
        _database.GetCollection<Product>("products");

    public IMongoCollection<Order> Orders =>
        _database.GetCollection<Order>("orders");

    public IMongoCollection<Category> Categories =>
        _database.GetCollection<Category>("categories");

    public IMongoCollection<Customer> Customers =>
        _database.GetCollection<Customer>("customers");

    public IMongoCollection<Customer> Customers2 =>
        _database.GetCollection<Customer>("customers2");

    public IMongoCollection<OutboxMessage> Outbox =>
        _database.GetCollection<OutboxMessage>("outbox");

    public IMongoCollection<Store> Stores =>
        _database.GetCollection<Store>("stores");

    public IMongoCollection<Organization> Organizations =>
        _database.GetCollection<Organization>("organizations");

    public IMongoCollection<Role> Roles =>
        _database.GetCollection<Role>("roles");

    public IMongoCollection<User> Users =>
        _database.GetCollection<User>("users");

    public IMongoCollection<UserResourceRole> UserResourceRoles =>
        _database.GetCollection<UserResourceRole>("user_resource_roles");

    public IMongoDatabase Database => _database;
}
