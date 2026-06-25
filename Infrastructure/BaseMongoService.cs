using System.Linq.Expressions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoApi.Filtering;
using MongoApi.Models;
using MongoApi.Models.Dtos;

namespace MongoApi.Infrastructure;

public abstract class BaseMongoService<T> : IBaseMongoService<T> where T : class, IDocument
{
    protected readonly IMongoCollection<T> _collection;

    protected BaseMongoService(IMongoCollection<T> collection)
    {
        _collection = collection;
    }

    /// <summary>
    /// Whitelist полей разрешённых для фильтрации и сортировки.
    /// Переопределяется в конкретном сервисе.
    /// </summary>
    protected abstract HashSet<string> AllowedFilterFields { get; }

    public virtual async Task<T?> GetByIdAsync(string id) =>
        await _collection.Find(x => x.Id == id).FirstOrDefaultAsync();

    public virtual async Task<T> CreateAsync(T entity)
    {
        await _collection.InsertOneAsync(entity);
        return entity;
    }

    public virtual async Task<bool> DeleteAsync(string id)
    {
        var result = await _collection.DeleteOneAsync(x => x.Id == id);
        return result.DeletedCount > 0;
    }

    /// <summary>
    /// Поиск с фильтрацией, сортировкой и пагинацией.
    /// Filter: "Price>100 AND Status==Active"
    /// Sort:   "Price:desc,Name:asc"
    /// </summary>
    public virtual async Task<PagedResult<T>> SearchAsync(QueryRequest request)
    {
        const int MaxPageSize = 100;
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var page = Math.Max(1, request.Page);

        var queryable = ApplyFilter(GetQueryable(), request.Filter);
        queryable = ApplySort(queryable, request.ParseSort());

        var total = queryable.Count();
        var items = queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return await Task.FromResult(new PagedResult<T>(items, page, pageSize, total));
    }

    public virtual async Task<PagedResult<T>> SearchAsync(QueryRequest request, Expression<Func<T, bool>> predicate)
    {
        const int MaxPageSize = 100;
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var page = Math.Max(1, request.Page);

        var queryable = ApplyFilter(GetQueryable().Where(predicate), request.Filter);
        queryable = ApplySort(queryable, request.ParseSort());

        var total = queryable.Count();
        var items = queryable
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return await Task.FromResult(new PagedResult<T>(items, page, pageSize, total));
    }

    public virtual IQueryable<T> GetQueryable() => _collection.AsQueryable();

    // -----------------------------------------------------------------------
    // Защищённые хелперы — используй в новых методах потомков
    // -----------------------------------------------------------------------

    /// <summary>
    /// Применяет строковый фильтр к IQueryable.
    /// </summary>
    protected IQueryable<T> ApplyFilter(IQueryable<T> queryable, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return queryable;

        var operations = new GeneralOperationType(AllowedFilterFields);
        var parser = new LogicalParser<T>(filter, operations);
        var predicate = parser.Parse();
        return queryable.Where(predicate);
    }

    /// <summary>
    /// Применяет LINQ-предикат к IQueryable.
    /// </summary>
    protected IQueryable<T> ApplyFilter(IQueryable<T> queryable, Expression<Func<T, bool>> predicate) =>
        queryable.Where(predicate);

    /// <summary>
    /// Применяет многоуровневую сортировку. Поля не из AllowedFilterFields пропускаются.
    /// "Price:desc,Name:asc" → OrderByDescending(Price).ThenBy(Name)
    /// </summary>
    protected IQueryable<T> ApplySort(IQueryable<T> queryable, SortField[] sortings)
    {
        IOrderedQueryable<T>? ordered = null;

        foreach (var sorting in sortings)
        {
            // Ищем каноническое имя поля (PascalCase) — case-insensitive
            var fieldName = AllowedFilterFields.FirstOrDefault(f =>
                f.Equals(sorting.Field, StringComparison.OrdinalIgnoreCase));
            if (fieldName is null)
                continue;

            var parameter = Expression.Parameter(typeof(T), "p");
            var property = Expression.Property(parameter, fieldName);
            var boxed = Expression.Convert(property, typeof(object));
            var keySelector = Expression.Lambda<Func<T, object>>(boxed, parameter);

            ordered = ordered is null
                ? sorting.Desc ? queryable.OrderByDescending(keySelector) : queryable.OrderBy(keySelector)
                : sorting.Desc ? ordered.ThenByDescending(keySelector)    : ordered.ThenBy(keySelector);
        }

        return ordered ?? queryable;
    }

    /// <summary>
    /// Строит Expression-предикат из строки фильтра.
    /// </summary>
    protected Expression<Func<T, bool>> BuildPredicate(string filter)
    {
        var operations = new GeneralOperationType(AllowedFilterFields);
        var parser = new LogicalParser<T>(filter, operations);
        return parser.Parse();
    }
}
