using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MongoApi.Services.Abstractions;

namespace MongoApi.Infrastructure.Authorization;

/// <summary>
/// Проверяет специфическое разрешение через IPermissionService.CheckPermissionAsync.
/// Аргументы: название разрешения + имена route-параметров (порядок важен — передаются в CheckPermissionAsync).
/// Пример:
///   [AuthorizePermission(Permissions.DeleteProduct, "productId", "orgId")]
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class AuthorizePermissionAttribute : ActionFilterAttribute
{
    private readonly string   _permission;
    private readonly string[] _routeParams;

    public AuthorizePermissionAttribute(string permission, params string[] routeParams)
    {
        _permission  = permission;
        _routeParams = routeParams;
    }

    public override async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var userId = context.HttpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var resourceIds = _routeParams
            .Select(p => context.RouteData.Values[p]?.ToString() ?? string.Empty)
            .ToArray();

        var permissionService = context.HttpContext.RequestServices
            .GetRequiredService<IPermissionService>();

        var allowed = await permissionService.CheckPermissionAsync(userId, _permission, resourceIds);

        if (!allowed)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
