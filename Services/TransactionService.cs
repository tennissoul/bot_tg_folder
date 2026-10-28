using System.Globalization;
using KeyBot.Models;

namespace KeyBot.Services;

public interface ITransactionService
{
    Task<bool> RecordTransactionAsync(long userId, string payload, decimal amount);
    Task RecordPendingPaymentAsync(
        string orderId,
        long userId,
        int days,
        int priceRub,
        string invoiceId,
        decimal paymentAmount
    );
    Task<PendingPayment?> GetPaymentAsync(string orderId);
    Task<bool> CompletePaymentAsync(
        string orderId,
        string invoiceId,
        decimal paymentAmount
    );
    Task<bool> MarkCustomerNotifiedAsync(string orderId);
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
        foreach (var transaction in _database.Transactions.Values)
        {
            if (transaction.Status == "completed")
                Interlocked.Add(ref _totalRevenue, transaction.Amount);
        }
    }

    public Task<bool> RecordTransactionAsync(long userId, string payload, decimal amount)
    {
        var tid = Interlocked.Increment(ref _transactionSeq).ToString();
        var roundedAmount = (int)amount;
        lock (_database.SyncRoot)
        {
            while (_database.Transactions.ContainsKey(tid))
                tid = Interlocked.Increment(ref _transactionSeq).ToString();

            _database.Transactions[tid] = new()
            {
                UserId = userId,
                Payload = payload,
                Amount = roundedAmount,
                Timestamp = DateTime.UtcNow.ToString("o"),
                Status = "completed",
            };
            Interlocked.Add(ref _totalRevenue, roundedAmount);
            if (_database.Users.TryGetValue(userId.ToString(), out var user))
                user.TotalSpent += roundedAmount;
        }
        return Task.FromResult(true);
    }

    public Task RecordPendingPaymentAsync(
        string orderId,
        long userId,
        int days,
        int priceRub,
        string invoiceId,
        decimal paymentAmount
    )
    {
        if (
            string.IsNullOrWhiteSpace(orderId)
            || userId <= 0
            || days is <= 0 or > 3650
            || priceRub <= 0
            || string.IsNullOrWhiteSpace(invoiceId)
            || paymentAmount <= 0
        )
            throw new ArgumentException("Payment order data is invalid.");

        lock (_database.SyncRoot)
        {
            if (_database.Transactions.ContainsKey(orderId))
                throw new InvalidOperationException("Payment order already exists.");

            _database.Transactions[orderId] = new()
            {
                UserId = userId,
                Payload = orderId,
                Amount = priceRub,
                TariffDays = days,
                InvoiceId = invoiceId,
                PaymentAmount = paymentAmount.ToString("0.00", CultureInfo.InvariantCulture),
                Timestamp = DateTime.UtcNow.ToString("o"),
                Status = "pending",
            };
        }

        return Task.CompletedTask;
    }

    public Task<PendingPayment?> GetPaymentAsync(string orderId)
    {
        lock (_database.SyncRoot)
        {
            if (
                !_database.Transactions.TryGetValue(orderId, out var transaction)
                || !decimal.TryParse(
                    transaction.PaymentAmount,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var paymentAmount
                )
            )
                return Task.FromResult<PendingPayment?>(null);

            return Task.FromResult<PendingPayment?>(
                new PendingPayment(
                    orderId,
                    transaction.UserId,
                    transaction.TariffDays,
                    transaction.Amount,
                    transaction.InvoiceId,
                    paymentAmount,
                    transaction.Status,
                    transaction.CustomerNotified
                )
            );
        }
    }

    public Task<bool> CompletePaymentAsync(
        string orderId,
        string invoiceId,
        decimal paymentAmount
    )
    {
        lock (_database.SyncRoot)
        {
            if (
                !_database.Transactions.TryGetValue(orderId, out var transaction)
                || transaction.InvoiceId != invoiceId
                || !decimal.TryParse(
                    transaction.PaymentAmount,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var expectedAmount
                )
                || expectedAmount != paymentAmount
            )
                return Task.FromResult(false);

            if (transaction.Status == "completed")
                return Task.FromResult(true);
            if (transaction.Status != "pending")
                return Task.FromResult(false);

            transaction.Status = "completed";
            transaction.Timestamp = DateTime.UtcNow.ToString("o");
            Interlocked.Add(ref _totalRevenue, transaction.Amount);
            if (_database.Users.TryGetValue(transaction.UserId.ToString(), out var user))
                user.TotalSpent += transaction.Amount;
            return Task.FromResult(true);
        }
    }

    public Task<bool> MarkCustomerNotifiedAsync(string orderId)
    {
        lock (_database.SyncRoot)
        {
            if (
                !_database.Transactions.TryGetValue(orderId, out var transaction)
                || transaction.Status != "completed"
            )
                return Task.FromResult(false);

            transaction.CustomerNotified = true;
            return Task.FromResult(true);
        }
    }

    public Task<List<TransactionModel>> GetUserTransactionsAsync(long userId)
    {
        var transactions = new List<TransactionModel>(8);
        lock (_database.SyncRoot)
        {
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
                        Days = kv.Value.TariffDays,
                        CreatedAt = DateTime.Parse(kv.Value.Timestamp),
                        Status = kv.Value.Status switch
                        {
                            "pending" => TransactionStatus.Pending,
                            "completed" => TransactionStatus.Completed,
                            "failed" => TransactionStatus.Failed,
                            _ => TransactionStatus.Failed,
                        },
                    }
                );
            }
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
