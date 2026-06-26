using Microsoft.AspNetCore.Identity;
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
        await CreateUserIndexesAsync();
        await CreateRoleIndexesAsync();
        await CreateUserOrganizationRoleIndexesAsync();
        await MigrateProductStatusAsync();
        await SeedRolesAsync();
        if (_env.IsDevelopment())
        {
            await SeedDevUserAsync();
            await EnableProfilerAsync();
        }
    }

    /// <summary>Роли — нужны в любом окружении. Idempotent.</summary>
    private async Task SeedRolesAsync()
    {
        var collection = _db.GetCollection<Role>("roles");
        var names = new[] { "Admin", "Member", "Viewer" };

        foreach (var name in names)
        {
            var exists = await collection.Find(r => r.Name == name).AnyAsync();
            if (!exists)
                await collection.InsertOneAsync(new Role { Name = name });
        }
    }

    /// <summary>Тестовый пользователь — только в Development. Idempotent.</summary>
    private async Task SeedDevUserAsync()
    {
        var collection = _db.GetCollection<User>("users");
        const string email = "admin@test.com";

        var exists = await collection.Find(u => u.Email == email).AnyAsync();
        if (exists) return;

        var hasher = new PasswordHasher<string>();
        await collection.InsertOneAsync(new User
        {
            Username     = "admin",
            FirstName    = "Admin",
            LastName     = "Dev",
            Email        = email,
            PasswordHash = hasher.HashPassword(email, "admin123"),
            IsActive     = true
        });

        Console.WriteLine("[Seed] Dev user created: admin@test.com / admin123");
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

    private async Task CreateUserIndexesAsync()
    {
        var collection = _db.GetCollection<User>("users");

        await collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true, Name = "idx_user_email_unique" }
            ),
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.Username),
                new CreateIndexOptions { Unique = true, Name = "idx_user_username_unique" }
            )
        });
    }

    private async Task CreateRoleIndexesAsync()
    {
        var collection = _db.GetCollection<Role>("roles");

        await collection.Indexes.CreateOneAsync(
            new CreateIndexModel<Role>(
                Builders<Role>.IndexKeys.Ascending(r => r.Name),
                new CreateIndexOptions { Unique = true, Name = "idx_role_name_unique" }
            )
        );
    }

    private async Task CreateUserOrganizationRoleIndexesAsync()
    {
        var collection = _db.GetCollection<UserOrganizationRole>("user_organization_roles");

        var indexes = new[]
        {
            // Compound unique: один пользователь — одна запись в организации
            new CreateIndexModel<UserOrganizationRole>(
                Builders<UserOrganizationRole>.IndexKeys
                    .Ascending(u => u.UserId)
                    .Ascending(u => u.OrganizationId),
                new CreateIndexOptions { Unique = true, Name = "idx_uor_user_org_unique" }
            ),
            // Быстрый поиск: "все члены организации"
            new CreateIndexModel<UserOrganizationRole>(
                Builders<UserOrganizationRole>.IndexKeys.Ascending(u => u.OrganizationId),
                new CreateIndexOptions { Name = "idx_uor_org" }
            ),
            // Быстрый поиск: "мои организации"
            new CreateIndexModel<UserOrganizationRole>(
                Builders<UserOrganizationRole>.IndexKeys.Ascending(u => u.UserId),
                new CreateIndexOptions { Name = "idx_uor_user" }
            )
        };

        await collection.Indexes.CreateManyAsync(indexes);
    }
}
