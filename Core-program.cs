using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using KeyBot.App;
using KeyBot.Configuration;

namespace KeyBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Устанавливаем UTF-8 для консоли Windows (чтобы не было кракозябр)
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch { }

            try
            {
                // Загружаем .env из корня проекта перед запуском
                LoadEnvFile();

                // Загружаем конфигурацию из переменных окружения
                var config = BotConfig.FromEnvironment();

                Console.WriteLine("🤖 Enkephalin Bot Starting...\n");
                Console.WriteLine($"✓ Bot Token: {config.BotToken[..10]}...");

                if (!string.IsNullOrEmpty(config.CryptoPayToken))
                {
                    Console.WriteLine($"✓ CryptoPay Token: {config.CryptoPayToken[..10]}...");
                }
                else
                {
                    Console.WriteLine(
                        "⚠️ CryptoPay Token не установлен - платежи работать не будут"
                    );
                }

                Console.WriteLine($"✓ Admin IDs: {string.Join(", ", config.AdminIds)}");
                Console.WriteLine();

                var botApp = new BotApplication(config);
                await botApp.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Critical error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine("\nНажми любую клавишу для выхода...");
                try
                {
                    Console.ReadKey();
                }
                catch { }
            }
        }

        private static void LoadEnvFile()
        {
            // Ищем .env в разных местах
            string[] possiblePaths = new string[]
            {
                // Корень проекта (где лежит .csproj)
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../.env"),
                // Текущая директория
                Path.Combine(Environment.CurrentDirectory, ".env"),
                // Папка с бинарником
                Path.Combine(AppContext.BaseDirectory, ".env"),
                // Desktop папка с проектом (абсолютный путь)
                "/home/tennissoul/Desktop/bot_tg_folder/.env",
                // Просто .env
                ".env",
            };

            string? envPath = null;
            foreach (var path in possiblePaths)
            {
                if (System.IO.File.Exists(path))
                {
                    envPath = path;
                    break;
                }
            }

            if (envPath != null && System.IO.File.Exists(envPath))
            {
                Console.WriteLine($"✓ Загружаем .env из {envPath}");
                foreach (var line in System.IO.File.ReadAllLines(envPath))
                {
                    var trimmedLine = line.Trim();
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
                Console.WriteLine("⚠️ .env файл не найден, ищем в:");
                foreach (var path in possiblePaths)
                {
                    Console.WriteLine($"   - {path}");
                }
            }
        }
    }
}
