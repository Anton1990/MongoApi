using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoApi.Models;

public enum ProductStatus
{
    Active,
    Discontinued,
    OutOfStock
}

public class Manufacturer
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("country")]
    public string Country { get; set; } = null!;
}

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

    /// <summary>
    /// Ссылка (Reference) на документ в коллекции "categories".
    /// Хранится как ObjectId в MongoDB, в C# — как string.
    /// </summary>
    [BsonElement("categoryId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string CategoryId { get; set; } = null!;

    [BsonElement("isAvailable")]
    public bool IsAvailable { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Версия документа для Optimistic Concurrency.
    /// При каждом UPDATE инкрементируется на 1.
    /// Клиент должен передавать ту версию которую получил при чтении.
    /// Если версия не совпадает — кто-то обновил документ раньше → 409 Conflict.
    /// </summary>
    [BsonElement("version")]
    public int Version { get; set; } = 0;

    [BsonElement("manufacturer")]
    public Manufacturer? Manufacturer { get; set; }

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public ProductStatus Status { get; set; } = ProductStatus.Active;
}
