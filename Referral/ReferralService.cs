namespace KeyBot.Services;

/// <summary>Реализация реферальной программы: коды, бонусы, статистика.</summary>
public class ReferralService : IReferralService
{
    private readonly Database _db;
    private readonly object _lock = new();

    private const int BonusDaysPerReferral = 7;
    private const int ReferreeBonusDays = 3;
    private const int MinTariffDaysForBonus = 30;

    public ReferralService(Database db, object? _ = null)
    {
        _db = db;
    }

    public string GetRefCode(long userId)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        Span<char> code = stackalloc char[8];
        long n = Math.Abs(userId) + 100000;
        int pos = 8;
        while (n > 0 && pos > 0)
        {
            code[--pos] = chars[(int)(n % chars.Length)];
            n /= chars.Length;
        }
        return new string(code[pos..]);
    }

    public Task<bool> RegisterReferralAsync(long referrerId, long refereeId)
    {
        if (referrerId == refereeId)
            return Task.FromResult(false);

        if (
            _db.Users.TryGetValue(refereeId.ToString(), out var userData)
            && userData.ReferredBy != null
        )
            return Task.FromResult(false);

        lock (_lock)
        {
            // Double-check after lock
            if (_db.Users.TryGetValue(refereeId.ToString(), out var u) && u.ReferredBy != null)
                return Task.FromResult(true);

            if (_db.Users.TryGetValue(refereeId.ToString(), out var user))
                user.ReferredBy = referrerId.ToString();

            var refs = _db.Referrals.GetOrAdd(referrerId.ToString(), _ => new List<ReferralData>());

            if (refs.Any(r => r.RefereeId == refereeId))
                return Task.FromResult(true);

            refs.Add(
                new ReferralData
                {
                    ReferrerId = referrerId,
                    RefereeId = refereeId,
                    RegisteredAt = DateTime.UtcNow.ToString("o"),
                }
            );
        }
        return Task.FromResult(true);
    }

    public Task AwardBonusAsync(long refereeId, int tariffDays)
    {
        if (tariffDays < MinTariffDaysForBonus)
            return Task.CompletedTask;

        if (
            !_db.Users.TryGetValue(refereeId.ToString(), out var userData)
            || userData.ReferredBy == null
        )
            return Task.CompletedTask;

        lock (_lock)
        {
            if (_db.Referrals.TryGetValue(userData.ReferredBy, out var refs))
            {
                var entry = refs.FirstOrDefault(r => r.RefereeId == refereeId);
                if (entry != null)
                {
                    entry.HasPurchased = true;
                    entry.BonusDaysPending += BonusDaysPerReferral;
                }
            }

            var referralKey = refereeId.ToString();
            bool isFirstPurchase = true;
            foreach (var list in _db.Referrals.Values)
            {
                if (
                    list.Any(r =>
                        r.RefereeId == refereeId && r.HasPurchased && r.BonusDaysAwarded > 0
                    )
                )
                {
                    isFirstPurchase = false;
                    break;
                }
            }

            if (isFirstPurchase)
                _db.ReferralBalance.AddOrUpdate(
                    referralKey,
                    ReferreeBonusDays,
                    (_, v) => v + ReferreeBonusDays
                );
        }
        return Task.CompletedTask;
    }

    public Task<int> ActivateBonusDaysAsync(long userId)
    {
        var key = userId.ToString();
        return Task.FromResult(
            _db.ReferralBalance.TryGetValue(key, out int days) && days > 0
                ? _db.ReferralBalance[key] = 0
                : 0
        );
    }

    public Task<ReferralStats> GetStatsAsync(long userId)
    {
        var key = userId.ToString();
        var r = _db.Referrals.TryGetValue(key, out var list) ? list : [];
        int pending = 0,
            earned = 0;
        foreach (var x in r)
        {
            pending += x.BonusDaysPending;
            earned += x.BonusDaysAwarded;
        }
        _db.ReferralBalance.TryGetValue(key, out int bal);
        return Task.FromResult(
            new ReferralStats(r.Count, r.Count(x => x.HasPurchased), bal, pending, earned)
        );
    }
}
