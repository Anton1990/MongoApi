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
        // Читает claim "sub" из JWT, выданного через POST /api/auth/login
        var userId = _accessor.HttpContext?.User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedException("Authentication required. Provide Bearer token.");

        return userId;
    }
}
