namespace MongoApi.Models.Dtos;

public record PagedResult<T>(
    List<T> Items,
    int Page,
    int PageSize,
    int Total
);
