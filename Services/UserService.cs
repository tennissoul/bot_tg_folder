using System.Text.Json;
using KeyBot.Models;

namespace KeyBot.Services;

public interface IUserService
{
    Task<UserModel?> GetUserAsync(long userId);
    Task<bool> AddUserAsync(long userId, string username);
    Task<bool> BanUserAsync(long userId);
    Task<bool> UnbanUserAsync(long userId);
    Task<bool> IsUserBannedAsync(long userId);
    Task<List<long>> GetAllUserIdsAsync();
    Task<UserLanguage> GetLanguageAsync(long userId);
    Task SetLanguageAsync(long userId, UserLanguage lang);
}

public class UserService : IUserService
{
    private readonly Database _database;
    private readonly object _banLock = new();

    public UserService(Database database, object? _ = null)
    {
        _database = database;
    }

    public Task<UserModel?> GetUserAsync(long userId)
    {
        if (!_database.Users.TryGetValue(userId.ToString(), out var d))
            return Task.FromResult<UserModel?>(null);
        return Task.FromResult<UserModel?>(
            new()
            {
                Id = userId,
                Username = d.Username,
                FirstSeen = DateTime.Parse(d.FirstSeen),
                PurchaseCount = d.Purchases,
                TotalSpent = d.TotalSpent,
            }
        );
    }

    public Task<bool> AddUserAsync(long userId, string username) =>
        Task.FromResult(
            _database.Users.TryAdd(
                userId.ToString(),
                new UserData
                {
                    UserId = userId,
                    Username = username,
                    FirstSeen = DateTime.UtcNow.ToString("o"),
                }
            )
        );

    public Task<bool> BanUserAsync(long userId)
    {
        lock (_banLock)
        {
            if (!_database.BannedUsers.Contains(userId))
                _database.BannedUsers.Add(userId);
        }
        return Task.FromResult(true);
    }

    public Task<bool> UnbanUserAsync(long userId)
    {
        lock (_banLock)
        {
            _database.BannedUsers.Remove(userId);
        }
        return Task.FromResult(true);
    }

    public Task<bool> IsUserBannedAsync(long userId)
    {
        lock (_banLock)
        {
            return Task.FromResult(_database.BannedUsers.Contains(userId));
        }
    }

    public Task<List<long>> GetAllUserIdsAsync()
    {
        var ids = new List<long>(_database.Users.Count);
        foreach (var k in _database.Users.Keys)
            if (long.TryParse(k, out var id))
                ids.Add(id);
        return Task.FromResult(ids);
    }

    public Task<UserLanguage> GetLanguageAsync(long userId) =>
        Task.FromResult(
            _database.Users.TryGetValue(userId.ToString(), out var u)
                ? u.Language == "en"
                    ? UserLanguage.English
                    : UserLanguage.Russian
                : UserLanguage.Russian
        );

    public Task SetLanguageAsync(long userId, UserLanguage lang)
    {
        if (_database.Users.TryGetValue(userId.ToString(), out var u))
            u.Language = lang == UserLanguage.English ? "en" : "ru";
        return Task.CompletedTask;
    }
}
