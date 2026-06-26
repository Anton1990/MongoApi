namespace MongoApi.Infrastructure;

public interface ICurrentUserService
{
    /// <summary>
    /// Возвращает userId текущего пользователя из X-User-Id header (или JWT sub claim).
    /// Бросает UnauthorizedException если header отсутствует.
    /// </summary>
    string GetUserId();
}
