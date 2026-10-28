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

    public UserService(Database database, object? _ = null)
    {
        _database = database;
    }

    public Task<UserModel?> GetUserAsync(long userId)
    {
        lock (_database.SyncRoot)
        {
            if (!_database.Users.TryGetValue(userId.ToString(), out var data))
                return Task.FromResult<UserModel?>(null);
            return Task.FromResult<UserModel?>(
                new()
                {
                    Id = userId,
                    Username = data.Username,
                    FirstSeen = DateTime.Parse(data.FirstSeen),
                    PurchaseCount = data.Purchases,
                    TotalSpent = data.TotalSpent,
                }
            );
        }
    }

    public Task<bool> AddUserAsync(long userId, string username)
    {
        if (userId <= 0)
            return Task.FromResult(false);

        lock (_database.SyncRoot)
        {
            return Task.FromResult(
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
        }
    }

    public Task<bool> BanUserAsync(long userId)
    {
        lock (_database.SyncRoot)
        {
            if (!_database.BannedUsers.Contains(userId))
                _database.BannedUsers.Add(userId);
        }
        return Task.FromResult(true);
    }

    public Task<bool> UnbanUserAsync(long userId)
    {
        lock (_database.SyncRoot)
        {
            _database.BannedUsers.Remove(userId);
        }
        return Task.FromResult(true);
    }

    public Task<bool> IsUserBannedAsync(long userId)
    {
        lock (_database.SyncRoot)
        {
            return Task.FromResult(_database.BannedUsers.Contains(userId));
        }
    }

    public Task<List<long>> GetAllUserIdsAsync()
    {
        lock (_database.SyncRoot)
        {
            var ids = new List<long>(_database.Users.Count);
            foreach (var key in _database.Users.Keys)
                if (long.TryParse(key, out var id))
                    ids.Add(id);
            return Task.FromResult(ids);
        }
    }

    public Task<UserLanguage> GetLanguageAsync(long userId) =>
        GetLanguageCore(userId);

    public Task SetLanguageAsync(long userId, UserLanguage lang)
    {
        lock (_database.SyncRoot)
        {
            if (_database.Users.TryGetValue(userId.ToString(), out var user))
                user.Language = lang == UserLanguage.English ? "en" : "ru";
        }
        return Task.CompletedTask;
    }

    private Task<UserLanguage> GetLanguageCore(long userId)
    {
        lock (_database.SyncRoot)
        {
            return Task.FromResult(
                _database.Users.TryGetValue(userId.ToString(), out var user)
                    ? user.Language == "en"
                        ? UserLanguage.English
                        : UserLanguage.Russian
                    : UserLanguage.Russian
            );
        }
    }
}
