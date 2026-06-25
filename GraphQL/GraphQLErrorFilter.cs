using MongoApi.Infrastructure.Exceptions;

namespace MongoApi.GraphQL;

/// <summary>
/// GraphQL error filter — трансформирует исключения в GraphQL errors[].
/// GraphQL всегда возвращает HTTP 200, ошибки — в теле ответа.
///
/// Ответ клиенту:
/// {
///   "errors": [{
///     "message": "Product 'abc123' not found",
///     "extensions": {
///       "code": "NotFound",
///       "stackTrace": "..."  // только в Development
///     }
///   }]
/// }
/// </summary>
public sealed class GraphQLErrorFilter(IHostEnvironment env) : IErrorFilter
{
    public IError OnError(IError error)
    {
        var (message, code) = error.Exception switch
        {
            AppException ex => (ex.Message, ex.ErrorCode.ToString()),
            null            => (error.Message, "Unknown"),
            _               => (env.IsProduction()
                                    ? "An unexpected error occurred."
                                    : error.Exception.Message,
                                ErrorCode.InternalError.ToString())
        };

        var builder = error
            .WithMessage(message)
            .WithCode(code);

        if (!env.IsProduction() && error.Exception?.StackTrace != null)
            builder = builder.SetExtension("stackTrace", error.Exception.StackTrace);

        return builder;
    }
}
