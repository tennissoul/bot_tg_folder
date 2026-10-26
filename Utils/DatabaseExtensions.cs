using System;
using System.IO;
using System.Text.Json;

namespace KeyBot.Utils;

public static class DatabaseExtensions
{
    public static void LoadFromJson(this Database database, string filePath)
    {
        try
        {
            if (System.IO.File.Exists(filePath))
            {
                string json = System.IO.File.ReadAllText(filePath);
                var options = JsonSetup.GetOptions();
                var loaded = JsonSerializer.Deserialize(json, BotJsonContext.Default.Database);

                if (loaded != null)
                {
                    database.Users = loaded.Users;
                    database.Keys = loaded.Keys;
                    database.Transactions = loaded.Transactions;
                    database.BannedUsers = loaded.BannedUsers;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка загрузки БД: {ex.Message}");
        }
    }

    public static void SaveToJson(this Database database, string filePath)
    {
        try
        {
            var options = JsonSetup.GetOptions();
            string json = JsonSerializer.Serialize(database, BotJsonContext.Default.Database);

            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            System.IO.File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка сохранения БД: {ex.Message}");
        }
    }
}
