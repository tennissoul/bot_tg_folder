using System;

namespace KeyBot
{
    public class UserData
    {
        public long UserId { get; set; }
        public string Username { get; set; } = "";
        public string FirstSeen { get; set; } = "";
        public int Purchases { get; set; }
        public int TotalSpent { get; set; }
        public string Language { get; set; } = "ru"; // "ru" | "en"
        public string? ReferredBy { get; set; } // userId пригласившего
    }

    public class TransactionData
    {
        public long UserId { get; set; }
        public string Payload { get; set; } = "";
        public int Amount { get; set; }
        public int TariffDays { get; set; }
        public string InvoiceId { get; set; } = "";
        public string PaymentAmount { get; set; } = "";
        public bool CustomerNotified { get; set; }
        public string Timestamp { get; set; } = "";
        public string Status { get; set; } = "completed";
    }

    public class KeyData
    {
        public long UserId { get; set; }
        public string Created { get; set; } = "";
        public string Expires { get; set; } = "";
        public bool Active { get; set; } = true;
        public int TariffDays { get; set; }
        public string? PaymentOrderId { get; set; }
        public string? FrozenAt { get; set; } // дата заморозки, null = активна
    }

    public class ReferralData
    {
        public long ReferrerId { get; set; }
        public long RefereeId { get; set; }
        public string RegisteredAt { get; set; } = "";
        public bool HasPurchased { get; set; }
        public int BonusDaysPending { get; set; }
        public int BonusDaysAwarded { get; set; }
    }
}
