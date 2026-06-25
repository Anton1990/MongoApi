using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Models;
using MongoApi.Services.Abstractions;

namespace MongoApi.Services;

public class StoreService : IStoreService
{
    private readonly IMongoCollection<Store> _collection;

    public StoreService(MongoDbContext context)
    {
        _collection = context.Stores;
    }

    public IQueryable<Store> GetQueryable() =>
        _collection.AsQueryable();

    /// <summary>
    /// Возвращает IQueryable<Store> отфильтрованный по ProductId.
    /// Hot Chocolate добавит сверху .Where(), .OrderBy(), .Skip(), .Take()
    /// — всё выполнится как один MongoDB запрос.
    /// </summary>
    public IQueryable<Store> GetQueryableByProductId(string productId) =>
        _collection.AsQueryable().Where(s => s.ProductId == productId);
}
