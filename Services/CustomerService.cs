using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Models;

namespace MongoApi.Services;

public class CustomerService
{
    private readonly IMongoCollection<Customer> _customers;

    public CustomerService(MongoDbContext context)
    {
        _customers = context.Customers2;
    }

    public async Task<List<Customer>> GetAllAsync() =>
        await _customers.Find(_ => true).ToListAsync();

    public async Task<Customer?> GetByIdAsync(string id) =>
        await _customers.Find(c => c.Id == id).FirstOrDefaultAsync();

    public async Task<Customer> CreateAsync(Customer customer)
    {
        await _customers.InsertOneAsync(customer);
        return customer;
    }

    public async Task<bool> UpdateAsync(string id, Customer updated)
    {
        updated.Id = id;
        var result = await _customers.ReplaceOneAsync(c => c.Id == id, updated);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _customers.DeleteOneAsync(c => c.Id == id);
        return result.DeletedCount > 0;
    }
}
