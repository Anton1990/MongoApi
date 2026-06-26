using MongoApi.Infrastructure.Exceptions;

namespace MongoApi.Infrastructure;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string GetUserId()
    {
        // Читает из ClaimsPrincipal (заполняется HeaderAuthHandler или JWT)
        var userId = _accessor.HttpContext?.User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedException("Authentication required. Provide X-User-Id header.");

        return userId;
    }
}
