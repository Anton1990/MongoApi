using MongoApi.Models.Dtos;

namespace MongoApi.Services.Abstractions;

public interface IAuthService
{
    Task<TokenResponse> LoginAsync(string username, string password);
}
