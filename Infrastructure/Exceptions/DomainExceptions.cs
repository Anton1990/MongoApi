namespace MongoApi.Infrastructure.Exceptions;

/// <summary>
/// 404 — ресурс не найден.
/// Пример: Product "abc123" not found
/// </summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string resource, string id)
        : base($"{resource} '{id}' not found", ErrorCode.NotFound) { }

    public NotFoundException(string message)
        : base(message, ErrorCode.NotFound) { }
}

/// <summary>
/// 409 — конфликт версий или дублирование.
/// Пример: Optimistic concurrency — версия документа изменилась
/// </summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message)
        : base(message, ErrorCode.Conflict) { }
}

/// <summary>
/// 400 — невалидный ввод или нарушение бизнес-правила.
/// Пример: Stock недостаточен для заказа
/// </summary>
public sealed class ValidationException : AppException
{
    public ValidationException(string message)
        : base(message, ErrorCode.Validation) { }
}

/// <summary>
/// 401 — не аутентифицирован.
/// </summary>
public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Authentication required")
        : base(message, ErrorCode.Unauthorized) { }
}

/// <summary>
/// 403 — нет прав на операцию.
/// </summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Access denied")
        : base(message, ErrorCode.Forbidden) { }
}
