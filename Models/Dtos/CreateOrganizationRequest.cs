namespace MongoApi.Models.Dtos;

public class CreateOrganizationRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>Id роли "Admin" — назначается создателю организации.</summary>
    public string AdminRoleId { get; set; } = null!;
}
