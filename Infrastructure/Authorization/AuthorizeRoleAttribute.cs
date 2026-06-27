using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MongoApi.Services.Abstractions;

namespace MongoApi.Infrastructure.Authorization;

/// <summary>
/// Проверяет роль пользователя в указанном ресурсе.
/// Читает resourceId из route по имени параметра.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class AuthorizeRoleAttribute : ActionFilterAttribute
{
    private readonly string _resourceType;
    private readonly string _routeParam;
    private readonly string _requiredRole;

    /// <param name="resourceType">Тип ресурса — ResourceType.Organization, ResourceType.Product и т.д.</param>
    /// <param name="routeParam">Имя route-параметра с id ресурса, например "orgId".</param>
    /// <param name="requiredRole">Минимальная роль — Roles.Admin, Roles.Member и т.д.</param>
    public AuthorizeRoleAttribute(string resourceType, string routeParam, string requiredRole)
    {
        _resourceType = resourceType;
        _routeParam   = routeParam;
        _requiredRole = requiredRole;
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

        var resourceId = context.RouteData.Values[_routeParam]?.ToString();
        if (string.IsNullOrEmpty(resourceId))
        {
            context.Result = new ForbidResult();
            return;
        }

        var permissionService = context.HttpContext.RequestServices
            .GetRequiredService<IPermissionService>();

        var allowed = await permissionService.HasPermissionAsync(
            userId, resourceId, _resourceType, _requiredRole);

        if (!allowed)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}

/// <summary>
/// OR-семантика: разрешает доступ если пользователь имеет нужную роль
/// хотя бы в одном из указанных ресурсов.
/// Аргументы идут тройками: resourceType1, routeParam1, role1, resourceType2, routeParam2, role2, ...
/// Пример:
///   [AuthorizeAnyRole(
///       ResourceType.Organization, "orgId",     Roles.Admin,
///       ResourceType.Product,      "productId", Roles.Admin)]
/// </summary>
public class AuthorizeAnyRoleAttribute : ActionFilterAttribute
{
    private readonly (string resourceType, string routeParam, string requiredRole)[] _checks;

    /// <param name="args">Тройки: resourceType, routeParam, requiredRole (повторяются для каждого условия)</param>
    public AuthorizeAnyRoleAttribute(params string[] args)
    {
        if (args.Length % 3 != 0)
            throw new ArgumentException("Args must be in groups of 3: resourceType, routeParam, requiredRole");

        _checks = new (string, string, string)[args.Length / 3];
        for (var i = 0; i < _checks.Length; i++)
            _checks[i] = (args[i * 3], args[i * 3 + 1], args[i * 3 + 2]);
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

        var permissionService = context.HttpContext.RequestServices
            .GetRequiredService<IPermissionService>();

        foreach (var (resourceType, routeParam, requiredRole) in _checks)
        {
            var resourceId = context.RouteData.Values[routeParam]?.ToString();
            if (string.IsNullOrEmpty(resourceId)) continue;

            var allowed = await permissionService.HasPermissionAsync(
                userId, resourceId, resourceType, requiredRole);

            if (allowed)
            {
                await next();
                return;
            }
        }

        context.Result = new ForbidResult();
    }
}
