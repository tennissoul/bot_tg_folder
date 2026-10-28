using System;
using System.IO;
using System.Linq;

namespace KeyBot.Configuration;

public class BotConfig
{
    public string BotToken { get; set; } = "";
    public string CryptoPayToken { get; set; } = "";
    public string CryptoPayWebhookUrl { get; set; } = "";
    public string CryptoPayWebhookListenUrl { get; set; } = "http://127.0.0.1:8081";
    public bool AllowTestPayments { get; set; }
    public string OpenRouterKey { get; set; } = "";
    public string BotUsername { get; set; } = "";
    public long[] AdminIds { get; set; } = Array.Empty<long>();
    public string SupportUsername { get; set; } = "";
    public string TermsUrl { get; set; } = "";
    public string DataPath { get; set; } = "data/bot_data.json";

    public static BotConfig FromEnvironment()
    {
        LoadEnvFile();

        var adminIdsValue = Environment.GetEnvironmentVariable("ADMIN_IDS");
        if (string.IsNullOrWhiteSpace(adminIdsValue))
            throw new InvalidOperationException("ADMIN_IDS must contain at least one Telegram user ID.");

        var adminIds = adminIdsValue
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseAdminId)
            .Distinct()
            .ToArray();
        if (adminIds.Length == 0)
            throw new InvalidOperationException("ADMIN_IDS must contain at least one Telegram user ID.");

        var botToken = Environment.GetEnvironmentVariable("BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new InvalidOperationException(
                "BOT_TOKEN is required. Set it in the environment or local .env file."
            );
        }

        var cryptoPayToken = Environment.GetEnvironmentVariable("CRYPTOPAY_TOKEN") ?? "";
        var cryptoPayWebhookUrl = Environment.GetEnvironmentVariable("CRYPTOPAY_WEBHOOK_URL") ?? "";
        var cryptoPayWebhookListenUrl =
            Environment.GetEnvironmentVariable("CRYPTOPAY_WEBHOOK_LISTEN_URL")
            ?? "http://127.0.0.1:8081";

        if (!string.IsNullOrWhiteSpace(cryptoPayToken))
        {
            if (
                !Uri.TryCreate(cryptoPayWebhookUrl, UriKind.Absolute, out var webhookUri)
                || webhookUri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(webhookUri.UserInfo)
                || !string.IsNullOrEmpty(webhookUri.Query)
                || !string.IsNullOrEmpty(webhookUri.Fragment)
                || webhookUri.AbsolutePath != "/webhooks/cryptopay"
            )
                throw new InvalidOperationException(
                    "CRYPTOPAY_WEBHOOK_URL must be an HTTPS URL ending in /webhooks/cryptopay."
                );

            if (
                !Uri.TryCreate(cryptoPayWebhookListenUrl, UriKind.Absolute, out var listenUri)
                || listenUri.Scheme != Uri.UriSchemeHttp
                || !string.IsNullOrEmpty(listenUri.UserInfo)
                || !string.IsNullOrEmpty(listenUri.Query)
                || !string.IsNullOrEmpty(listenUri.Fragment)
                || listenUri.AbsolutePath != "/"
                || !IsLoopbackHost(listenUri.Host)
            )
                throw new InvalidOperationException(
                    "CRYPTOPAY_WEBHOOK_LISTEN_URL must be an HTTP loopback URL."
                );
        }

        return new BotConfig
        {
            BotToken = botToken,
            CryptoPayToken = cryptoPayToken,
            CryptoPayWebhookUrl = cryptoPayWebhookUrl,
            CryptoPayWebhookListenUrl = cryptoPayWebhookListenUrl,
            AllowTestPayments = string.Equals(
                Environment.GetEnvironmentVariable("ALLOW_TEST_PAYMENTS"),
                "true",
                StringComparison.OrdinalIgnoreCase
            ),
            OpenRouterKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "",
            BotUsername = Environment.GetEnvironmentVariable("BOT_USERNAME") ?? "bot",
            AdminIds = adminIds,
            SupportUsername = Environment.GetEnvironmentVariable("SUPPORT_USERNAME") ?? "CherrysXD",
            TermsUrl = Environment.GetEnvironmentVariable("TERMS_URL") ?? "https://telegra.ph",
            DataPath = Path.GetFullPath(
                Environment.GetEnvironmentVariable("DATA_PATH") ?? "data/bot_data.json"
            ),
        };
    }

    private static long ParseAdminId(string value)
    {
        if (!long.TryParse(value, out var adminId) || adminId <= 0)
            throw new InvalidOperationException("ADMIN_IDS contains an invalid Telegram user ID.");

        return adminId;
    }

    private static bool IsLoopbackHost(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (
            System.Net.IPAddress.TryParse(host, out var address)
            && System.Net.IPAddress.IsLoopback(address)
        );

    private static void LoadEnvFile()
    {
        string envPath = FindEnvFile();

        if (!string.IsNullOrEmpty(envPath) && System.IO.File.Exists(envPath))
        {
            foreach (var line in System.IO.File.ReadAllLines(envPath))
            {
                var trimmedLine = line.Trim();

                if (
                    string.IsNullOrEmpty(trimmedLine)
                    || trimmedLine.StartsWith("#", StringComparison.Ordinal)
                )
                    continue;

                var separatorIndex = trimmedLine.IndexOf('=');
                if (separatorIndex > 0)
                {
                    var key = trimmedLine.Substring(0, separatorIndex).Trim();
                    var value = trimmedLine.Substring(separatorIndex + 1).Trim();

                    // Убираем кавычки, если есть
                    if (value.StartsWith("\"") && value.EndsWith("\""))
                        value = value.Substring(1, value.Length - 2);

                    if (Environment.GetEnvironmentVariable(key) is null)
                        Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }

    private static string FindEnvFile()
    {
        // Ищем .env в разных местах
        string[] possiblePaths = new string[]
        {
            Path.Combine(AppContext.BaseDirectory, ".env"),
            Path.Combine(Environment.CurrentDirectory, ".env"),
        };

        foreach (var path in possiblePaths)
        {
            if (System.IO.File.Exists(path))
                return path;
        }

        return "";
    }
}
