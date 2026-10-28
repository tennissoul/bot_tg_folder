namespace KeyBot.Models;

public sealed record CryptoPayInvoice(string InvoiceId, string PayUrl, decimal Amount);
