using System.Collections.Concurrent;

namespace KeyBot
{
    /// <summary>Хранилище данных в памяти. Все коллекции thread-safe.</summary>
    public class Database
    {
        public ConcurrentDictionary<string, UserData> Users { get; set; } = new();
        public ConcurrentDictionary<string, TransactionData> Transactions { get; set; } = new();
        public ConcurrentDictionary<string, KeyData> Keys { get; set; } = new();
        public List<long> BannedUsers { get; set; } = new();

        // key = referrerId, value = list of referrals
        public ConcurrentDictionary<string, List<ReferralData>> Referrals { get; set; } = new();

        // key = userId, bonus days balance
        public ConcurrentDictionary<string, int> ReferralBalance { get; set; } = new();
    }
}
