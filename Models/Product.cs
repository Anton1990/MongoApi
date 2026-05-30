using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoApi.Models;

public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("price")]
    public decimal Price { get; set; }

    [BsonElement("stock")]
    public int Stock { get; set; }

    [BsonElement("category")]
    public string Category { get; set; } = null!;

    [BsonElement("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
