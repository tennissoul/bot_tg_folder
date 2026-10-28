using System.Security.Cryptography;
using KeyBot.Models;

namespace KeyBot.Services;

public interface IKeyService
{
    Task<string> GenerateKeyAsync(long userId, int days);
    Task<string> GeneratePaidKeyAsync(string orderId, long userId, int days);
    Task<string?> GetPaidKeyAsync(string orderId);
    Task<KeyModel?> GetKeyAsync(string key, long userId);
    Task<List<KeyModel>> GetUserKeysAsync(long userId);
    Task<bool> RevokeKeyAsync(string key, long userId);
    Task<bool> FreezeKeyAsync(string key, long userId);
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
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));
        if (days is <= 0 or > 3650)
            throw new ArgumentOutOfRangeException(nameof(days));

        lock (_database.SyncRoot)
            return Task.FromResult(CreateKeyLocked(userId, days, null));
    }

    public Task<string> GeneratePaidKeyAsync(string orderId, long userId, int days)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new ArgumentException("Payment order ID is required.", nameof(orderId));
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));
        if (days is <= 0 or > 3650)
            throw new ArgumentOutOfRangeException(nameof(days));

        lock (_database.SyncRoot)
        {
            foreach (var (key, keyData) in _database.Keys)
            {
                if (keyData.PaymentOrderId == orderId)
                    return Task.FromResult(key);
            }

            return Task.FromResult(CreateKeyLocked(userId, days, orderId));
        }
    }

    public Task<string?> GetPaidKeyAsync(string orderId)
    {
        lock (_database.SyncRoot)
        {
            foreach (var (key, keyData) in _database.Keys)
            {
                if (keyData.PaymentOrderId == orderId)
                    return Task.FromResult<string?>(key);
            }

            return Task.FromResult<string?>(null);
        }
    }

    public Task<KeyModel?> GetKeyAsync(string key, long userId)
    {
        lock (_database.SyncRoot)
        {
            if (_database.Keys.TryGetValue(key, out var keyData) && keyData.UserId == userId)
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
    }

    public Task<List<KeyModel>> GetUserKeysAsync(long userId)
    {
        var keys = new List<KeyModel>(4);
        var now = DateTime.UtcNow;
        lock (_database.SyncRoot)
        {
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
                                kv.Value.FrozenAt != null
                                    ? DateTime.Parse(kv.Value.FrozenAt)
                                    : null,
                        }
                    );
                }
            }
        }
        return Task.FromResult(keys);
    }

    public Task<bool> RevokeKeyAsync(string key, long userId)
    {
        lock (_database.SyncRoot)
        {
            if (_database.Keys.TryGetValue(key, out var keyData) && keyData.UserId == userId)
            {
                keyData.Active = false;
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public Task<bool> FreezeKeyAsync(string key, long userId)
    {
        lock (_database.SyncRoot)
        {
            if (
                _database.Keys.TryGetValue(key, out var keyData)
                && keyData.UserId == userId
                && keyData.Active
            )
            {
                keyData.FrozenAt = DateTime.UtcNow.ToString("o");
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }

    public Task<int> RemoveUserSubscriptionsAsync(long userId)
    {
        int count = 0;
        lock (_database.SyncRoot)
        {
            foreach (var kv in _database.Keys)
            {
                if (kv.Value.UserId == userId && kv.Value.Active)
                {
                    kv.Value.Active = false;
                    count++;
                }
            }
        }
        return Task.FromResult(count);
    }

    private static string GenerateRandomKey()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        Span<char> result = stackalloc char[32];
        for (int i = 0; i < 32; i++)
        {
            result[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }

        return new string(result);
    }

    private string CreateKeyLocked(long userId, int days, string? paymentOrderId)
    {
        var expiresAt = DateTime.UtcNow.AddDays(days);
        string key;
        do
        {
            key = GenerateRandomKey();
        } while (!_database.Keys.TryAdd(
            key,
            new KeyBot.KeyData
            {
                UserId = userId,
                Created = DateTime.UtcNow.ToString("o"),
                Expires = expiresAt.ToString("o"),
                Active = true,
                TariffDays = days,
                PaymentOrderId = paymentOrderId,
                FrozenAt = null,
            }
        ));

        if (_database.Users.TryGetValue(userId.ToString(), out var user))
            user.Purchases++;
        return key;
    }
}
