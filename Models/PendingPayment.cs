namespace KeyBot.Models;

public sealed record PendingPayment(
    string OrderId,
    long UserId,
    int Days,
    int PriceRub,
    string InvoiceId,
    decimal PaymentAmount,
    string Status,
    bool CustomerNotified
);
