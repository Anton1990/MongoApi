namespace MongoApi.Models.Dtos;

public class AddMemberRequest
{
    public string UserId { get; set; } = null!;
    public string RoleId { get; set; } = null!;
}
