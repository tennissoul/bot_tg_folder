using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KeyBot.Utils;

public static class DatabaseExtensions
{
    public static void LoadFromJson(this Database database, string filePath)
    {
        if (!System.IO.File.Exists(filePath))
            return;

        string json = System.IO.File.ReadAllText(filePath);
        var loaded = JsonSerializer.Deserialize(json, BotJsonContext.Default.Database);

        if (loaded == null)
            throw new InvalidDataException("Database file contains no database document.");

        lock (database.SyncRoot)
        {
            database.Users = loaded.Users;
            database.Keys = loaded.Keys;
            database.Transactions = loaded.Transactions;
            database.BannedUsers = loaded.BannedUsers;
            database.Referrals = loaded.Referrals;
            database.ReferralBalance = loaded.ReferralBalance;
        }
    }

    public static async Task SaveToJsonAsync(
        this Database database,
        string filePath,
        CancellationToken cancellationToken
    )
    {
        byte[] json;
        lock (database.SyncRoot)
            json = JsonSerializer.SerializeToUtf8Bytes(database, BotJsonContext.Default.Database);

        string fullPath = Path.GetFullPath(filePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (
                var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous
                )
            )
            {
                await stream.WriteAsync(json, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
