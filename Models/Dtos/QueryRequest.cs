namespace MongoApi.Models.Dtos;

public class QueryRequest
{
    public string? Filter { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Сортировка: "Price:desc,Name:asc"
    /// Поля разделяются запятой, направление через двоеточие (asc по умолчанию).
    /// </summary>
    public string? Sort { get; set; }

    public SortField[] ParseSort() =>
        string.IsNullOrWhiteSpace(Sort)
            ? []
            : Sort.Split(',', StringSplitOptions.RemoveEmptyEntries)
                  .Select(part =>
                  {
                      var segments = part.Trim().Split(':', 2);
                      var field = segments[0].Trim();
                      var desc = segments.Length > 1 &&
                                 segments[1].Trim().Equals("desc", StringComparison.OrdinalIgnoreCase);
                      return new SortField(field, desc);
                  })
                  .ToArray();
}

public record SortField(string Field, bool Desc);
