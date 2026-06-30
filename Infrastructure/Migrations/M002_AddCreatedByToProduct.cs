using MongoDB.Bson;
using MongoDB.Driver;

namespace MongoApi.Infrastructure.Migrations;

/// <summary>Добавляет поле createdBy = null для существующих продуктов.</summary>
public class M002_AddCreatedByToProduct : IMigration
{
    public string Version => "002";
    public string Name    => "AddCreatedByToProduct";

    public async Task UpAsync(IMongoDatabase db)
    {
        var products = db.GetCollection<BsonDocument>("products");
        await products.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Exists("createdBy", false),
            Builders<BsonDocument>.Update.Set("createdBy", BsonNull.Value));
    }

    public async Task DownAsync(IMongoDatabase db)
    {
        var products = db.GetCollection<BsonDocument>("products");
        await products.UpdateManyAsync(
            Builders<BsonDocument>.Filter.Exists("createdBy", true),
            Builders<BsonDocument>.Update.Unset("createdBy"));
    }
}
