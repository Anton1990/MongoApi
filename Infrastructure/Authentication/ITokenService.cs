using System.Security.Claims;
using MongoApi.Models;

namespace MongoApi.Infrastructure.Authentication;

public interface ITokenService
{
    /// <summary>Создаёт access + refresh токены для пользователя.</summary>
    (string AccessToken, string RefreshToken) Create(User user);

    /// <summary>Валидирует токен и возвращает ClaimsPrincipal. Бросает исключение если невалидный.</summary>
    ClaimsPrincipal Validate(string token);
}
