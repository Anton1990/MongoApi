using MongoDB.Driver;

namespace MongoApi.Infrastructure.Migrations;

public interface IMigration
{
    string Version { get; }  // "001", "002", ...
    string Name    { get; }  // "AddProductStatus"
    Task UpAsync(IMongoDatabase db);
    Task DownAsync(IMongoDatabase db);
}
