using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoApi.Models;

/// <summary>
/// Junction: User + Organization + Role.
/// Один документ = "Этот пользователь имеет эту роль в этой организации".
/// Аналог UserRole в EnterAR.
/// </summary>
public class UserOrganizationRole : IDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("userId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    [BsonElement("organizationId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string OrganizationId { get; set; } = null!;

    [BsonElement("roleId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string RoleId { get; set; } = null!;

    [BsonElement("assignedAt")]
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
