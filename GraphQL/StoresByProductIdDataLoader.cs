using GreenDonut;
using MongoDB.Driver;
using MongoApi.Models;
using MongoApi.Services.Abstractions;

namespace MongoApi.GraphQL;

/// <summary>
/// DataLoader батчит запросы к stores.
///
/// Вместо N запросов (по одному на каждый продукт):
///   db.stores.find({ productId: "id-1" })
///   db.stores.find({ productId: "id-2" })
///
/// Делает ОДИН запрос для всех продуктов:
///   db.stores.find({ productId: { $in: ["id-1", "id-2", ...] } })
/// </summary>
public class StoresByProductIdDataLoader : BatchDataLoader<string, Store[]>
{
    private readonly IStoreService _storeService;

    public StoresByProductIdDataLoader(
        IStoreService storeService,
        IBatchScheduler batchScheduler,
        DataLoaderOptions options)
        : base(batchScheduler, options)
    {
        _storeService = storeService;
    }

    protected override async Task<IReadOnlyDictionary<string, Store[]>> LoadBatchAsync(
        IReadOnlyList<string> productIds,
        CancellationToken cancellationToken)
    {
        // ОДИН запрос для всех productIds
        var stores = _storeService
            .GetQueryable()
            .Where(s => productIds.Contains(s.ProductId))
            .ToList();

        return stores
            .GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }
}
