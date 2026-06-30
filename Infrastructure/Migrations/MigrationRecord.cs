using MongoDB.Bson.Serialization.Attributes;

namespace MongoApi.Infrastructure.Migrations;

public class MigrationRecord
{
    [BsonId]
    public string   Version   { get; set; } = null!;
    public string   Name      { get; set; } = null!;
    public DateTime AppliedAt { get; set; }
}
