using MongoApi.Infrastructure;
using MongoApi.Models;

namespace MongoApi.Services.Abstractions;

public interface IProductService : IBaseMongoService<Product>
{
    Task<Product> UpdateAsync(string id, Product updated);
}
