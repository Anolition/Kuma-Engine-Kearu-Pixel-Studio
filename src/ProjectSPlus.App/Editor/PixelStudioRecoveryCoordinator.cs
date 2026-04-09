namespace ProjectSPlus.App.Editor;

public static class PixelStudioRecoveryCoordinator
{
    private static readonly object Sync = new();
    private static Func<bool>? _flushCallback;

    public static void Register(Func<bool> flushCallback)
    {
        ArgumentNullException.ThrowIfNull(flushCallback);
        lock (Sync)
        {
            _flushCallback = flushCallback;
        }
    }

    public static void Unregister(Func<bool> flushCallback)
    {
        lock (Sync)
        {
            if (_flushCallback == flushCallback)
            {
                _flushCallback = null;
            }
        }
    }

    public static bool TryFlushPendingRecovery()
    {
        Func<bool>? callback;
        lock (Sync)
        {
            callback = _flushCallback;
        }

        if (callback is null)
        {
            return false;
        }

        try
        {
            return callback();
        }
        catch
        {
            return false;
        }
    }
}
