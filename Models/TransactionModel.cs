namespace KeyBot.Models;

public class TransactionModel
{
    public string Id { get; set; } = "";
    public long UserId { get; set; }
    public decimal Amount { get; set; }
    public int Days { get; set; }
    public DateTime CreatedAt { get; set; }
    public TransactionStatus Status { get; set; }
    public string PaymentMethod { get; set; } = "CryptoPay";
}

public enum TransactionStatus
{
    Pending,
    Completed,
    Failed,
    Refunded,
}
