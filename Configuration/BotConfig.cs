using System;
using System.IO;
using System.Linq;

namespace KeyBot.Configuration;

public class BotConfig
{
    public string BotToken { get; set; } = "";
    public string CryptoPayToken { get; set; } = "";
    public string OpenRouterKey { get; set; } = "";
    public string BotUsername { get; set; } = "";
    public long[] AdminIds { get; set; } = Array.Empty<long>();
    public string SupportUsername { get; set; } = "";
    public string TermsUrl { get; set; } = "";
    public string DataPath { get; set; } = "data/bot_data.json";

    public static BotConfig FromEnvironment()
    {
        // Загружаем .env файл вручную (без DotNetEnv)
        LoadEnvFile();

        var adminIds = (Environment.GetEnvironmentVariable("ADMIN_IDS") ?? "5309079204")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(id => long.Parse(id.Trim()))
            .ToArray();

        var botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
        if (string.IsNullOrEmpty(botToken))
        {
            Console.WriteLine("\n❌ ОШИБКА: BOT_TOKEN не найден!");
            Console.WriteLine("\nЧтобы исправить:");
            Console.WriteLine("1. Создайте файл .env в папке проекта:");
            Console.WriteLine("   nano .env");
            Console.WriteLine("\n2. И добавьте туда:");
            Console.WriteLine("   BOT_TOKEN=8840484865:AAEr5f814m0ZJIGHNQaOW85FYJiZsqUsGXU");
            Console.WriteLine("   ADMIN_IDS=5309079204");
            Console.WriteLine("\n3. Или экспортируйте переменную:");
            Console.WriteLine("   export BOT_TOKEN=8840484865:AAEr5f814m0ZJIGHNQaOW85FYJiZsqUsGXU");
            Console.WriteLine("   dotnet run\n");
            throw new InvalidOperationException("BOT_TOKEN not set in environment or .env file");
        }

        return new BotConfig
        {
            BotToken = botToken,
            CryptoPayToken = Environment.GetEnvironmentVariable("CRYPTOPAY_TOKEN") ?? "",
            OpenRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "",
            BotUsername = Environment.GetEnvironmentVariable("BOT_USERNAME") ?? "bot",
            AdminIds = adminIds,
            SupportUsername = Environment.GetEnvironmentVariable("SUPPORT_USERNAME") ?? "CherrysXD",
            TermsUrl = Environment.GetEnvironmentVariable("TERMS_URL") ?? "https://telegra.ph",
            DataPath = Environment.GetEnvironmentVariable("DATA_PATH") ?? "data/bot_data.json",
        };
    }

    private static void LoadEnvFile()
    {
        string envPath = FindEnvFile();

        if (!string.IsNullOrEmpty(envPath) && System.IO.File.Exists(envPath))
        {
            Console.WriteLine($"✓ Загружаем конфигурацию из {envPath}");

            foreach (var line in System.IO.File.ReadAllLines(envPath))
            {
                var trimmedLine = line.Trim();

                // Пропускаем пустые строки и комментарии
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                var separatorIndex = trimmedLine.IndexOf('=');
                if (separatorIndex > 0)
                {
                    var key = trimmedLine.Substring(0, separatorIndex).Trim();
                    var value = trimmedLine.Substring(separatorIndex + 1).Trim();

                    // Убираем кавычки, если есть
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                        value = value.Substring(1, value.Length - 2);

                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
        else
        {
            Console.WriteLine("⚠️ .env файл не найден, используем системные переменные");
            Console.WriteLine($"   Искали в: {Environment.CurrentDirectory}");
        }
    }

    private static string FindEnvFile()
    {
        // Ищем .env в разных местах
        string[] possiblePaths = new string[]
        {
            Path.Combine(Environment.CurrentDirectory, ".env"),
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
            ".env",
        };

        foreach (var path in possiblePaths)
        {
            if (System.IO.File.Exists(path))
                return path;
        }

        return "";
    }
}
