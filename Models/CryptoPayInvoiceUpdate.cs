namespace KeyBot.Models;

public sealed record CryptoPayInvoiceUpdate(
    string OrderId,
    string InvoiceId,
    string Asset,
    decimal Amount
);
