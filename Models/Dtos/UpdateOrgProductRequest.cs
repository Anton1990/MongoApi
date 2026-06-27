namespace MongoApi.Models.Dtos;

public class UpdateOrgProductRequest
{
    public string  Name              { get; set; } = null!;
    public decimal Price             { get; set; }
    public int     Stock             { get; set; }
    public string  CategoryId        { get; set; } = null!;
    public ProductStatus Status      { get; set; } = ProductStatus.Active;
    public string? ManufacturerName    { get; set; }
    public string? ManufacturerCountry { get; set; }

    /// <summary>
    /// Версия документа, полученная при чтении.
    /// Используется для Optimistic Concurrency — передайте ту версию, что получили.
    /// При несовпадении вернётся 409 Conflict.
    /// </summary>
    public int Version { get; set; }
}
