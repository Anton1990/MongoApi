using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MongoApi.Infrastructure.Exceptions;

/// <summary>
/// Глобальный обработчик исключений для REST API.
/// Регистрируется один раз в Program.cs — убирает try/catch из контроллеров.
///
/// Возвращает ProblemDetails (RFC 7807):
/// {
///   "status": 404,
///   "title": "Not Found",
///   "detail": "Product 'abc123' not found",
///   "instance": "/api/products/abc123",
///   "extensions": { "code": "NotFound" }
/// }
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context,
        Exception exception,
        CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException   => (404, "Not Found"),
            ConflictException   => (409, "Conflict"),
            ValidationException => (400, "Bad Request"),
            UnauthorizedException => (401, "Unauthorized"),
            ForbiddenException  => (403, "Forbidden"),
            AppException        => (500, "Application Error"),
            _                   => (500, "Internal Server Error")
        };

        context.Response.StatusCode = status;

        // В продакшене скрываем детали 500 ошибок
        var detail = status == 500 && env.IsProduction()
            ? "An unexpected error occurred."
            : exception.Message;

        // type и code берём из AppException если это доменная ошибка
        var (type, code) = exception is AppException appEx
            ? (appEx.ProblemType, appEx.ErrorCode.ToString())
            : ("https://errors.mongoapi.com/internal-error", ErrorCode.InternalError.ToString());

        var problemDetails = new ProblemDetails
        {
            Type = type,       // ← RFC 7807 "type" URI
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["code"] = code;

        // traceId добавляется автоматически через IProblemDetailsService
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
