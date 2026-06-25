using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MongoApi.Models;

public class Customer : IDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("firstName")]
    public string FirstName { get; set; } = null!;

    [BsonElement("lastName")]
    public string LastName { get; set; } = null!;

    [BsonElement("email")]
    public string Email { get; set; } = null!;

    [BsonElement("phone")]
    public string? Phone { get; set; }

    [BsonElement("address")]
    public Address? Address { get; set; }

    [BsonElement("registeredAt")]
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

public class Address
{
    [BsonElement("street")]
    public string Street { get; set; } = null!;

    [BsonElement("city")]
    public string City { get; set; } = null!;

    [BsonElement("country")]
    public string Country { get; set; } = null!;
}
