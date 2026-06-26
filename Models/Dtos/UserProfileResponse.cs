namespace MongoApi.Models.Dtos;

public class UserProfileResponse
{
    public string   Id        { get; set; } = null!;
    public string   Username  { get; set; } = null!;
    public string   FirstName { get; set; } = null!;
    public string   LastName  { get; set; } = null!;
    public string   Email     { get; set; } = null!;
    public bool     IsActive  { get; set; }
    public DateTime CreatedAt { get; set; }
}
