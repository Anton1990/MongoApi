using MongoApi.Infrastructure;
using MongoApi.Models;

namespace MongoApi.Services.Abstractions;

public interface ICustomerService : IBaseMongoService<Customer>
{
    Task<bool> UpdateAsync(string id, Customer updated);
}
