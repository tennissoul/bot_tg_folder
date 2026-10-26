using KeyBot.Models;

namespace KeyBot.Services;

public interface ITransactionService
{
    Task<bool> RecordTransactionAsync(long userId, string payload, decimal amount);
    Task<List<TransactionModel>> GetUserTransactionsAsync(long userId);
    Task<decimal> GetTotalRevenueAsync();
}

public class TransactionService : ITransactionService
{
    private readonly Database _database;
    private long _transactionSeq; // монотонный счётчик вместо Guid
    private long _totalRevenue; // running total — без полного сканирования

    public TransactionService(Database database, object? _ = null)
    {
        _database = database;
        // Восстанавливаем revenue из сохранённых данных при старте
        foreach (var t in _database.Transactions.Values)
            Interlocked.Add(ref _totalRevenue, t.Amount);
    }

    public Task<bool> RecordTransactionAsync(long userId, string payload, decimal amount)
    {
        var tid = Interlocked.Increment(ref _transactionSeq).ToString();
        var a = (int)amount;
        _database.Transactions[tid] = new()
        {
            UserId = userId,
            Payload = payload,
            Amount = a,
            Timestamp = DateTime.UtcNow.ToString("o"),
            Status = "completed",
        };
        Interlocked.Add(ref _totalRevenue, a);
        if (_database.Users.TryGetValue(userId.ToString(), out var u))
            u.TotalSpent += a;
        return Task.FromResult(true);
    }

    public Task<List<TransactionModel>> GetUserTransactionsAsync(long userId)
    {
        var transactions = new List<TransactionModel>(8);
        foreach (var kv in _database.Transactions)
        {
            if (kv.Value.UserId != userId)
                continue;
            transactions.Add(
                new TransactionModel
                {
                    Id = kv.Key,
                    UserId = kv.Value.UserId,
                    Amount = kv.Value.Amount,
                    CreatedAt = DateTime.Parse(kv.Value.Timestamp),
                    Status =
                        kv.Value.Status == "completed"
                            ? TransactionStatus.Completed
                            : TransactionStatus.Failed,
                }
            );
        }
        transactions.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));

        // Держим не более 50 транзакций в памяти на пользователя
        if (transactions.Count > 50)
            transactions = transactions.GetRange(0, 50);

        return Task.FromResult(transactions);
    }

    // O(1) — не сканируем все транзакции
    public Task<decimal> GetTotalRevenueAsync() =>
        Task.FromResult((decimal)Volatile.Read(ref _totalRevenue));
}
