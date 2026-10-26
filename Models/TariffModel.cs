namespace KeyBot.Models;

public class TariffModel
{
    public int Days { get; set; }
    public decimal PriceRub { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public static class Tariffs
{
    public static readonly TariffModel[] Available = new[]
    {
        new TariffModel
        {
            Days = 1,
            PriceRub = 10,
            Name = "Пробный день",
        },
        new TariffModel
        {
            Days = 30,
            PriceRub = 399,
            Name = "1 мес",
            Description = "(-36%)",
        },
        new TariffModel
        {
            Days = 90,
            PriceRub = 1149,
            Name = "3 мес",
            Description = "(-39%)",
        },
        new TariffModel
        {
            Days = 180,
            PriceRub = 2190,
            Name = "🔥 6 мес",
            Description = "(-42%)",
        },
        new TariffModel
        {
            Days = 365,
            PriceRub = 3999,
            Name = "1 год",
            Description = "(-55%)",
        },
        new TariffModel
        {
            Days = 730,
            PriceRub = 3999,
            Name = "💎 2 года",
            Description = "(-70%)",
        },
    };

    public static TariffModel? GetByDays(int days) => Available.FirstOrDefault(t => t.Days == days);
}
