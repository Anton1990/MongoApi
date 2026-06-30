using MongoDB.Bson;
using MongoDB.Driver;

namespace MongoApi.Infrastructure.Migrations;

/// <summary>Устанавливает status = "Active" для продуктов у которых поле отсутствует.</summary>
public class M001_AddProductStatus : IMigration
{
    public string Version => "001";
    public string Name    => "AddProductStatus";

    public async Task UpAsync(IMongoDatabase db)
    {
        var products = db.GetCollection<BsonDocument>("products");
        await products.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Exists("status", false),
            Builders<BsonDocument>.Update.Set("status", "Active"));
    }

    public async Task DownAsync(IMongoDatabase db)
    {
        var products = db.GetCollection<BsonDocument>("products");
        await products.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Exists("status", true),
            Builders<BsonDocument>.Update.Unset("status"));
    }
}
