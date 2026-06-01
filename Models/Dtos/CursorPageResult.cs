namespace MongoApi.Models.Dtos;

/// <summary>
/// Результат курсорной пагинации.
/// NextCursor — Id последнего элемента на странице.
/// Клиент передаёт его в следующем запросе чтобы получить следующую порцию.
/// </summary>
public record CursorPageResult<T>(
    List<T> Items,
    string? NextCursor,  // null — это последняя страница, больше данных нет
    bool HasMore
);
