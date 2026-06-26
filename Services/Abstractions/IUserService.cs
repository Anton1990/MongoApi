using MongoApi.Infrastructure;
using MongoApi.Models;

namespace MongoApi.Services.Abstractions;

public interface IUserService : IBaseMongoService<User>
{
    Task<User> CreateAsync(User user, string password);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByUsernameAsync(string username);
}
