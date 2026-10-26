namespace KeyBot.Services;

/// <summary>Статистика реферальной программы.</summary>
public record ReferralStats(
    int TotalReferred,
    int TotalPurchased,
    int BalanceDays,
    int PendingDays,
    int TotalEarnedDays
);
