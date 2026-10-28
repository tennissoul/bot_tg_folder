using System;
using System.Threading.Tasks;
using KeyBot.App;
using KeyBot.Configuration;

namespace KeyBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                var config = BotConfig.FromEnvironment();

                Console.WriteLine("🤖 Merchant Bot Starting...\n");
                Console.WriteLine("✓ Bot Token: configured");

                if (!string.IsNullOrEmpty(config.CryptoPayToken))
                {
                    Console.WriteLine("✓ CryptoPay Token: configured");
                }
                else
                {
                    Console.WriteLine(
                        "⚠️ CryptoPay Token не установлен - платежи работать не будут"
                    );
                }

                Console.WriteLine($"✓ Admin accounts configured: {config.AdminIds.Length}");
                Console.WriteLine();

                var botApp = new BotApplication(config);
                await botApp.RunAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"❌ Critical error ({ex.GetType().Name})"
                );
                Environment.ExitCode = 1;
            }
        }
    }
}
