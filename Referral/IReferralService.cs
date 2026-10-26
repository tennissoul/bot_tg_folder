namespace KeyBot.Services;

/// <summary>Сервис реферальной программы.</summary>
public interface IReferralService
{
    /// <summary>Генерирует реферальный код для пользователя (детерминированный).</summary>
    string GetRefCode(long userId);

    /// <summary>Регистрирует реферала. Возвращает false если уже зарегистрирован.</summary>
    Task<bool> RegisterReferralAsync(long referrerId, long refereeId);

    /// <summary>Начисляет бонусные дни рефереру при покупке реферала.</summary>
    Task AwardBonusAsync(long refereeId, int tariffDays);

    /// <summary>Активирует накопленные бонусные дни (добавляет к подписке).</summary>
    Task<int> ActivateBonusDaysAsync(long userId);

    /// <summary>Возвращает статистику реферальной программы пользователя.</summary>
    Task<ReferralStats> GetStatsAsync(long userId);
}
