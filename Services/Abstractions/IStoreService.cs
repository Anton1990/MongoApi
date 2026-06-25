using MongoApi.Models;

namespace MongoApi.Services.Abstractions;

public interface IStoreService
{
    IQueryable<Store> GetQueryable();
    IQueryable<Store> GetQueryableByProductId(string productId);
}
