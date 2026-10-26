namespace KeyBot.Models;

public class UserModel
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public DateTime FirstSeen { get; set; }
    public int PurchaseCount { get; set; }
    public decimal TotalSpent { get; set; }
    public bool IsBanned { get; set; }
}
