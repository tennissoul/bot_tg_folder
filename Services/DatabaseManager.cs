using KeyBot.Utils;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KeyBot.Services;

/// <summary>Управление загрузкой и сохранением базы данных.</summary>
public class DatabaseManager
{
    private readonly Database _database;
    private readonly string _dataPath;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private CancellationTokenSource? _saveCancellation;
    private Task? _saveTask;

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
        catch (IOException ex)
        {
            Console.Error.WriteLine($"❌ Не удалось загрузить БД: {ex.GetType().Name}");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"❌ Не удалось загрузить БД: {ex.GetType().Name}");
            throw;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"❌ Не удалось загрузить БД: {ex.GetType().Name}");
            throw;
        }
    }

    public async Task<bool> SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            await _database.SaveToJsonAsync(_dataPath, cancellationToken);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidOperationException
        )
        {
            Console.Error.WriteLine($"❌ Ошибка сохранения БД: {ex.GetType().Name}");
            return false;
        }
        finally
        {
            _saveGate.Release();
        }
    }

    /// <summary>Автосохранение каждые N мс. По умолчанию 30 секунд.</summary>
    public void StartAutoSave(int intervalMs = 30_000)
    {
        if (intervalMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(intervalMs));
        if (_saveTask is not null)
            throw new InvalidOperationException("Database auto-save is already running.");

        _saveCancellation = new CancellationTokenSource();
        _saveTask = RunAutoSaveAsync(TimeSpan.FromMilliseconds(intervalMs), _saveCancellation.Token);
    }

    public async Task StopAsync()
    {
        if (_saveCancellation is not null)
        {
            _saveCancellation.Cancel();
            if (_saveTask is not null)
            {
                try
                {
                    await _saveTask;
                }
                catch (OperationCanceledException) when (_saveCancellation.IsCancellationRequested) { }
            }

            _saveCancellation.Dispose();
            _saveCancellation = null;
            _saveTask = null;
        }

        if (!await SaveAsync())
            throw new InvalidOperationException("The database could not be saved during shutdown.");
        Console.WriteLine($"✓ БД сохранена в {_dataPath}");
    }

    private async Task RunAutoSaveAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
                await SaveAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }
}
