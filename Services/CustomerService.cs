using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Models;
using MongoApi.Services.Abstractions;

namespace MongoApi.Services;

public class CustomerService : BaseMongoService<Customer>, ICustomerService
{
    public CustomerService(MongoDbContext context)
        : base(context.Customers2) { }

    protected override HashSet<string> AllowedFilterFields => new()
    {
        nameof(Customer.FirstName),
        nameof(Customer.LastName),
        nameof(Customer.Email)
    };

    public async Task<bool> UpdateAsync(string id, Customer updated)
    {
        updated.Id = id;
        var result = await _collection.ReplaceOneAsync(c => c.Id == id, updated);
        return result.ModifiedCount > 0;
    }
}
