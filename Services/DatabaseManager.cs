using KeyBot.Utils;

namespace KeyBot.Services;

/// <summary>Управление загрузкой и сохранением базы данных.</summary>
public class DatabaseManager
{
    private readonly Database _database;
    private readonly string _dataPath;
    private Timer? _saveTimer;

    public DatabaseManager(Database database, string dataPath)
    {
        _database = database;
        _dataPath = dataPath;
    }

    public void Load()
    {
        try
        {
            _database.LoadFromJson(_dataPath);
            Console.WriteLine($"✓ БД загружена из {_dataPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Ошибка загрузки БД: {ex.Message}");
        }
    }

    public void Save()
    {
        try
        {
            _database.SaveToJson(_dataPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка сохранения БД: {ex.Message}");
        }
    }

    /// <summary>Автосохранение каждые N мс. По умолчанию 30 секунд.</summary>
    public void StartAutoSave(int intervalMs = 30_000)
    {
        _saveTimer?.Dispose();
        _saveTimer = new Timer(_ => Save(), null, intervalMs, intervalMs);
    }

    public void Stop()
    {
        _saveTimer?.Dispose();
        _saveTimer = null;
        Save();
        Console.WriteLine($"✓ БД сохранена в {_dataPath}");
    }
}
