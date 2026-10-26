namespace KeyBot.Models;

public class KeyModel
{
    public string Key { get; set; } = "";
    public long UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsActive { get; set; }
    public int TariffDays { get; set; }
    public DateTime? FrozenAt { get; set; }
    public bool IsFrozen => FrozenAt.HasValue;
}
