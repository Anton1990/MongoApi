namespace MongoApi.Infrastructure.Exceptions;

/// <summary>
/// Базовый класс для всех доменных исключений приложения.
///
/// Несёт три вещи которые нужны клиенту:
///   ErrorCode  → машиночитаемый код ("NotFound", "Conflict")
///   ProblemType → URI типа проблемы (RFC 7807 "type" field)
///
/// Пример ответа:
/// {
///   "type": "https://errors.mongoapi.com/not-found",
///   "title": "Not Found",
///   "status": 404,
///   "detail": "Product 'abc123' not found",
///   "code": "NotFound",
///   "traceId": "0HN7Q9T..."
/// }
/// </summary>
public abstract class AppException : Exception
{
    private const string BaseUri = "https://errors.mongoapi.com";

    public ErrorCode ErrorCode { get; }

    /// <summary>
    /// URI типа проблемы — используется в ProblemDetails.Type (RFC 7807).
    /// По умолчанию генерируется из ErrorCode.
    /// Каждый наследник может переопределить для специфичных ошибок.
    /// </summary>
    public virtual string ProblemType =>
        $"{BaseUri}/{ErrorCode.ToString().ToLower()}";

    protected AppException(string message, ErrorCode errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
