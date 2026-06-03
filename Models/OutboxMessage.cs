using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoApi.Models;

public class OutboxMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string RoutingKey { get; set; } = string.Empty;  // "product.created"
    public string Payload { get; set; } = string.Empty;     // JSON события
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool Published { get; set; } = false;
}
