using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoApi.Models.Dtos;

/// <summary>
/// Результат агрегации products + $lookup categories.
/// Поля должны точно совпадать с именами полей в MongoDB-документе после $lookup/$unwind.
/// </summary>
public class ProductWithCategoryDto
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

    [BsonElement("isAvailable")]
    public bool IsAvailable { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("categoryId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string CategoryId { get; set; } = null!;

    /// <summary>
    /// Заполняется после $lookup + $unwind.
    /// Null — если категория не найдена (preserveNullAndEmptyArrays: true).
    /// </summary>
    [BsonElement("category")]
    public Category? Category { get; set; }
}
