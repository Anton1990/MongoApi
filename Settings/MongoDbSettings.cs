namespace MongoApi.Settings;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string ProductsCollection { get; set; } = null!;
    public string CustomersCollection { get; set; } = null!;
}
