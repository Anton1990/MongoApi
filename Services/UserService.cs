using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Exceptions;
using MongoApi.Models;
using MongoApi.Services.Abstractions;

namespace MongoApi.Services;

public class UserService : BaseMongoService<User>, IUserService
{
    private readonly PasswordHasher<string> _hasher = new();

    public UserService(MongoDbContext context)
        : base(context.Users) { }

    protected override HashSet<string> AllowedFilterFields => new()
    {
        nameof(User.Username),
        nameof(User.FirstName),
        nameof(User.LastName),
        nameof(User.Email)
    };

    public override async Task<User> CreateAsync(User user)
    {
        throw new InvalidOperationException("Use CreateAsync(user, password) instead.");
    }

    public async Task<User> CreateAsync(User user, string password)
    {
        var existing = await _collection.Find(u => u.Email == user.Email).FirstOrDefaultAsync();
        if (existing != null)
            throw new ConflictException($"User with email '{user.Email}' already exists.");

        user.PasswordHash = _hasher.HashPassword(user.Email, password);
        await _collection.InsertOneAsync(user);
        return user;
    }

    public async Task<User?> GetByEmailAsync(string email) =>
        await _collection.Find(u => u.Email == email).FirstOrDefaultAsync();

    public async Task<User?> GetByUsernameAsync(string username) =>
        await _collection.Find(u => u.Username == username).FirstOrDefaultAsync();
}
