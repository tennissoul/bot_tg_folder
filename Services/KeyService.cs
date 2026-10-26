using System.Security.Cryptography;
using System.Text;
using KeyBot.Models;

namespace KeyBot.Services;

public interface IKeyService
{
    Task<string> GenerateKeyAsync(long userId, int days);
    Task<KeyModel?> GetKeyAsync(string key);
    Task<List<KeyModel>> GetUserKeysAsync(long userId);
    Task<bool> RevokeKeyAsync(string key);
    Task<bool> FreezeKeyAsync(string key); // заморозить подписку
    Task<int> RemoveUserSubscriptionsAsync(long userId); // удалить все подписки юзера
}

public class KeyService : IKeyService
{
    private readonly Database _database;

    public KeyService(Database database, object? _ = null)
    {
        _database = database;
    }

    public Task<string> GenerateKeyAsync(long userId, int days)
    {
        string key = GenerateRandomKey();
        var expiresAt = DateTime.UtcNow.AddDays(days);

        var userIdStr = userId.ToString();
        _database.Keys[key] = new KeyBot.KeyData
        {
            UserId = userId,
            Created = DateTime.UtcNow.ToString("o"),
            Expires = expiresAt.ToString("o"),
            Active = true,
            TariffDays = days,
            FrozenAt = null,
        };

        if (_database.Users.TryGetValue(userIdStr, out var user))
            user.Purchases++;

        return Task.FromResult(key);
    }

    public Task<KeyModel?> GetKeyAsync(string key)
    {
        if (_database.Keys.TryGetValue(key, out var keyData))
        {
            return Task.FromResult<KeyModel?>(
                new KeyModel
                {
                    Key = key,
                    UserId = keyData.UserId,
                    CreatedAt = DateTime.Parse(keyData.Created),
                    ExpiresAt = DateTime.Parse(keyData.Expires),
                    IsActive = keyData.Active,
                    TariffDays = keyData.TariffDays,
                    FrozenAt = keyData.FrozenAt != null ? DateTime.Parse(keyData.FrozenAt) : null,
                }
            );
        }
        return Task.FromResult<KeyModel?>(null);
    }

    public Task<List<KeyModel>> GetUserKeysAsync(long userId)
    {
        var keys = new List<KeyModel>(4);
        var now = DateTime.UtcNow;
        foreach (var kv in _database.Keys)
        {
            if (kv.Value.UserId == userId && kv.Value.Active)
            {
                var expiresAt = DateTime.Parse(kv.Value.Expires);
                keys.Add(
                    new KeyModel
                    {
                        Key = kv.Key,
                        UserId = kv.Value.UserId,
                        CreatedAt = DateTime.Parse(kv.Value.Created),
                        ExpiresAt = expiresAt,
                        IsActive = expiresAt > now,
                        TariffDays = kv.Value.TariffDays,
                        FrozenAt =
                            kv.Value.FrozenAt != null ? DateTime.Parse(kv.Value.FrozenAt) : null,
                    }
                );
            }
        }
        return Task.FromResult(keys);
    }

    public Task<bool> RevokeKeyAsync(string key)
    {
        if (_database.Keys.TryGetValue(key, out var keyData))
        {
            keyData.Active = false;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> FreezeKeyAsync(string key)
    {
        if (_database.Keys.TryGetValue(key, out var keyData) && keyData.Active)
        {
            keyData.FrozenAt = DateTime.UtcNow.ToString("o");
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<int> RemoveUserSubscriptionsAsync(long userId)
    {
        int count = 0;
        foreach (var kv in _database.Keys)
        {
            if (kv.Value.UserId == userId && kv.Value.Active)
            {
                kv.Value.Active = false;
                count++;
            }
        }
        return Task.FromResult(count);
    }

    private static string GenerateRandomKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        Span<char> result = stackalloc char[32];
        var rng = Random.Shared;

        for (int i = 0; i < 32; i++)
        {
            result[i] = chars[rng.Next(chars.Length)];
        }

        return new string(result);
    }
}
