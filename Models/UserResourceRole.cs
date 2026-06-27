using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoApi.Models;

/// <summary>
/// Универсальная junction: User + Resource + Role.
/// Работает для любой сущности (org, project, product и т.д.)
/// </summary>
public class UserResourceRole : IDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    [BsonElement("resourceId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ResourceId { get; set; } = null!;

    /// <summary>Тип ресурса — "org", "project", "product" и т.д.</summary>
    [BsonElement("resourceType")]
    public string ResourceType { get; set; } = null!;

    /// <summary>Имя роли — "Admin", "Member", "Viewer".</summary>
    [BsonElement("roleName")]
    public string RoleName { get; set; } = null!;

    [BsonElement("assignedAt")]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
