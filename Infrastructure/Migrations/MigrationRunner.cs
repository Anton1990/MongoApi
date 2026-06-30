using MongoDB.Driver;

namespace MongoApi.Infrastructure.Migrations;

public class MigrationRunner
{
    private readonly IMongoDatabase                    _db;
    private readonly IMongoCollection<MigrationRecord> _history;
    private readonly IReadOnlyList<IMigration>         _migrations;

    public MigrationRunner(MongoDbContext context)
    {
        _db      = context.Database;
        _history = _db.GetCollection<MigrationRecord>("_migrations");

        // Автообнаружение всех IMigration через reflection, отсортированных по версии
        _migrations = typeof(MigrationRunner).Assembly
            .GetTypes()
            .Where(t => typeof(IMigration).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t => (IMigration)Activator.CreateInstance(t)!)
            .OrderBy(m => m.Version)
            .ToList();
    }

    /// <summary>Применить все непримененные миграции.</summary>
    public async Task UpAsync()
    {
        var applied = await GetAppliedVersionsAsync();
        var pending = _migrations.Where(m => !applied.Contains(m.Version)).ToList();

        if (pending.Count == 0)
        {
            Console.WriteLine("No pending migrations.");
            return;
        }

        foreach (var migration in pending)
        {
            Console.Write($"  Applying [{migration.Version}] {migration.Name}... ");
            await migration.UpAsync(_db);
            await _history.InsertOneAsync(new MigrationRecord
            {
                Version   = migration.Version,
                Name      = migration.Name,
                AppliedAt = DateTime.UtcNow
            });
            Console.WriteLine("done");
        }
    }

    /// <summary>Откатить последние N миграций (по умолчанию 1).</summary>
    public async Task DownAsync(int steps = 1)
    {
        var applied = await _history
            .Find(_ => true)
            .SortByDescending(r => r.Version)
            .Limit(steps)
            .ToListAsync();

        if (applied.Count == 0)
        {
            Console.WriteLine("Nothing to roll back.");
            return;
        }

        foreach (var record in applied)
        {
            var migration = _migrations.FirstOrDefault(m => m.Version == record.Version);
            if (migration is null)
            {
                Console.WriteLine($"  [{record.Version}] {record.Name} — not found in code, skipping.");
                continue;
            }

            Console.Write($"  Rolling back [{record.Version}] {record.Name}... ");
            await migration.DownAsync(_db);
            await _history.DeleteOneAsync(r => r.Version == record.Version);
            Console.WriteLine("done");
        }
    }

    /// <summary>Показать историю миграций.</summary>
    public async Task StatusAsync()
    {
        var applied        = await _history.Find(_ => true).SortBy(r => r.Version).ToListAsync();
        var appliedByVer   = applied.ToDictionary(r => r.Version);

        Console.WriteLine();
        Console.WriteLine("Migration status:");
        Console.WriteLine(new string('-', 55));

        foreach (var migration in _migrations)
        {
            appliedByVer.TryGetValue(migration.Version, out var record);
            var mark = record is not null ? "✓" : " ";
            var date = record is not null ? record.AppliedAt.ToString("yyyy-MM-dd HH:mm") : "pending";
            Console.WriteLine($"  [{mark}] {migration.Version}  {migration.Name,-30}  {date}");
        }

        Console.WriteLine();
    }

    private async Task<HashSet<string>> GetAppliedVersionsAsync()
    {
        var records = await _history.Find(_ => true).ToListAsync();
        return records.Select(r => r.Version).ToHashSet();
    }
}
