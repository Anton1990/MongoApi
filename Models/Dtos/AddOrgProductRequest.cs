namespace MongoApi.Models.Dtos;

public class AddOrgProductRequest
{
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string CategoryId { get; set; } = null!;
    public string? ManufacturerName { get; set; }
    public string? ManufacturerCountry { get; set; }
}
