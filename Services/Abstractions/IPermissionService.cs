namespace MongoApi.Services.Abstractions;

public interface IPermissionService
{
    /// <summary>
    /// Проверяет имеет ли пользователь нужную роль в ресурсе.
    /// Один запрос в БД — RoleName денормализован в UserResourceRole.
    /// </summary>
    Task<bool> HasPermissionAsync(
        string userId,
        string resourceId,
        string resourceType,
        string requiredRole);

    /// <summary>
    /// Проверяет специфическое разрешение с кастомной логикой.
    /// Используется из AuthorizePermissionAttribute.
    /// resourceIds — route-параметры в том же порядке что указаны в атрибуте.
    /// </summary>
    Task<bool> CheckPermissionAsync(
        string userId,
        string permission,
        string[] resourceIds);
}
