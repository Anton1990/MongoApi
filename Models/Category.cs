using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoApi.Models;

public class Category
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("slug")]
    public string Slug { get; set; } = null!;

    [BsonElement("description")]
    public string? Description { get; set; }

    public BsonDocument? Payload { get; set; }
}
