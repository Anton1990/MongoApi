using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoApi.Models;

public class Store : IDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("address")]
    public string Address { get; set; } = null!;

    [BsonElement("city")]
    public string City { get; set; } = null!;

    /// <summary>
    /// Ссылка на Product. Один продукт — много магазинов (One-to-Many).
    /// </summary>
    [BsonElement("productId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ProductId { get; set; } = null!;

    [BsonElement("stock")]
    public int Stock { get; set; }

    [BsonElement("isOpen")]
    public bool IsOpen { get; set; } = true;
}
