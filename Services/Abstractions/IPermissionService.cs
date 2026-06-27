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
}
