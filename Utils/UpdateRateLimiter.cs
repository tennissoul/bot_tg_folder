namespace KeyBot.Utils;

internal sealed class UpdateRateLimiter
{
    private static readonly TimeSpan WindowLength = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);
    private const int UserLimit = 8;
    private const int GlobalLimit = 120;

    private readonly object _gate = new();
    private readonly Dictionary<long, Window> _userWindows = new();
    private Window _globalWindow;
    private DateTimeOffset _nextCleanup = DateTimeOffset.UtcNow + CleanupInterval;

    public bool TryAcquire(long userId)
    {
        var now = DateTimeOffset.UtcNow;

        lock (_gate)
        {
            if (IsFull(_globalWindow, now, GlobalLimit))
                return false;

            _userWindows.TryGetValue(userId, out var userWindow);
            if (IsFull(userWindow, now, UserLimit))
                return false;

            _globalWindow = Increment(_globalWindow, now);
            _userWindows[userId] = Increment(userWindow, now);

            if (now >= _nextCleanup)
                RemoveExpiredWindows(now);

            return true;
        }
    }

    private static bool IsFull(Window window, DateTimeOffset now, int limit) =>
        now - window.StartedAt < WindowLength && window.Count >= limit;

    private static Window Increment(Window window, DateTimeOffset now)
    {
        if (now - window.StartedAt >= WindowLength)
            return new Window(now, 1);

        return new Window(window.StartedAt, window.Count + 1);
    }

    private void RemoveExpiredWindows(DateTimeOffset now)
    {
        foreach (var userId in _userWindows.Keys.ToArray())
        {
            if (now - _userWindows[userId].StartedAt >= WindowLength)
                _userWindows.Remove(userId);
        }

        _nextCleanup = now + CleanupInterval;
    }

    private readonly record struct Window(DateTimeOffset StartedAt, int Count);
}
