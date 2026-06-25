using HotChocolate.Data;
using MongoApi.Models;
using MongoApi.Services.Abstractions;

namespace MongoApi.GraphQL;

[ExtendObjectType(typeof(Product))]
public class ProductExtensions
{
    /// <summary>
    /// Вариант 1: IQueryable — DB-level фильтрация/сортировка/пагинация.
    /// Минус: N+1 проблема (отдельный запрос на каждый продукт).
    /// Используй когда запрашиваешь один продукт.
    /// </summary>
    [UseOffsetPaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<Store> GetStores(
        [Parent] Product product,
        [Service] IStoreService storeService)
    {
        return storeService.GetQueryableByProductId(product.Id!);
    }

    /// <summary>
    /// Вариант 2: DataLoader — батчит все запросы в ОДИН.
    /// db.stores.find({ productId: { $in: ["id-1", "id-2", ...] } })
    /// Используй когда запрашиваешь список продуктов (избегает N+1).
    /// Минус: теряешь IQueryable → фильтрация/пагинация in-memory.
    /// </summary>
    public async Task<Store[]> GetStoresBatched(
        [Parent] Product product,
        StoresByProductIdDataLoader dataLoader)
    {
        return await dataLoader.LoadAsync(product.Id!);
    }
}
