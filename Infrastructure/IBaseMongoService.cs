using System.Linq.Expressions;
using MongoApi.Models;
using MongoApi.Models.Dtos;

namespace MongoApi.Infrastructure;

public interface IBaseMongoService<T> where T : class, IDocument
{
    Task<T?> GetByIdAsync(string id);
    Task<T> CreateAsync(T entity);
    Task<bool> DeleteAsync(string id);
    Task<PagedResult<T>> SearchAsync(QueryRequest request);
    Task<PagedResult<T>> SearchAsync(QueryRequest request, Expression<Func<T, bool>> predicate);
    IQueryable<T> GetQueryable();
}
