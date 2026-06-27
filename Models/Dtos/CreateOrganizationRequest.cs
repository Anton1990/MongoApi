namespace MongoApi.Models.Dtos;

public class CreateOrganizationRequest
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

}
