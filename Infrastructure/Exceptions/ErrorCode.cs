namespace MongoApi.Infrastructure.Exceptions;

/// <summary>
/// Машиночитаемые коды ошибок.
/// Возвращаются в:
///   REST   → ProblemDetails.Extensions["code"]
///   GraphQL → errors[].extensions.code
/// </summary>
public enum ErrorCode
{
    NotFound,
    Conflict,
    Validation,
    Unauthorized,
    Forbidden,
    InternalError
}
