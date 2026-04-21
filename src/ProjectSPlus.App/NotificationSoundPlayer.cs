using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ProjectSPlus.Core.Configuration;

namespace ProjectSPlus.App;

internal static class NotificationSoundPlayer
{
    private const uint WindowsWarningBeep = 0x00000030;
    private const uint WindowsCrashBeep = 0x00000010;
    private static readonly object PlaybackSync = new();
    private static readonly List<MediaPlayback> ActivePlaybacks = [];
    private static readonly SoundClip WarningClip = new("warning-frog-secondary.mp3", 0.08, 1.08);
    private static readonly SoundClip CrashClip = new("crash-bear.mp3", 0, null);

    public static EditorNotificationSoundMode SoundMode { get; set; } = EditorNotificationSoundMode.Custom;

    public static void PlayWarning()
    {
        if (SoundMode == EditorNotificationSoundMode.None)
        {
            return;
        }

        if (SoundMode == EditorNotificationSoundMode.Windows)
        {
            TryPlayWindowsBeep(WindowsWarningBeep);
            return;
        }

        if (!PlayClip(WarningClip, WindowsWarningBeep))
        {
            TryPlayWindowsBeep(WindowsWarningBeep);
        }
    }

    public static void PlayCrash()
    {
        if (SoundMode == EditorNotificationSoundMode.None)
        {
            return;
        }

        if (SoundMode == EditorNotificationSoundMode.Windows)
        {
            TryPlayWindowsBeep(WindowsCrashBeep);
            return;
        }

        if (!PlayClip(CrashClip, WindowsCrashBeep))
        {
            TryPlayWindowsBeep(WindowsCrashBeep);
        }
    }

    private static bool PlayClip(SoundClip clip, uint fallbackBeep)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        string clipPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", clip.FileName);
        if (!File.Exists(clipPath))
        {
            return false;
        }

#pragma warning disable CA1416
        _ = Task.Run(() => TryPlayMediaPlayerClip(
            clipPath,
            Math.Max(clip.StartSeconds, 0d),
            clip.DurationSeconds is null ? null : Math.Max(clip.DurationSeconds.Value, 0.25d),
            fallbackBeep));
#pragma warning restore CA1416
        return true;
    }

    [SupportedOSPlatform("windows")]
    private static void TryPlayMediaPlayerClip(string clipPath, double startSeconds, double? durationSeconds, uint fallbackBeep)
    {
        try
        {
            Type? playerType = Type.GetTypeFromProgID("WMPlayer.OCX");
            if (playerType is null)
            {
                TryPlayWindowsBeep(fallbackBeep);
                return;
            }

            object player = Activator.CreateInstance(playerType)
                ?? throw new InvalidOperationException("Unable to create WMPlayer.OCX instance.");
            object settings = GetProperty(player, "settings");
            object controls = GetProperty(player, "controls");

            SetProperty(settings, "volume", 100);
            InvokeMethod(settings, "setMode", "loop", false);
            SetProperty(player, "URL", clipPath);

            MediaPlayback playback = new(player, controls);
            RegisterPlayback(playback);

            playback.StartTimer = new Timer(
                _ => BeginPlayback(playback, startSeconds, durationSeconds),
                null,
                dueTime: 140,
                period: Timeout.Infinite);
        }
        catch (Exception ex)
        {
            LogPlaybackFailure(ex);
            TryPlayWindowsBeep(fallbackBeep);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void BeginPlayback(MediaPlayback playback, double startSeconds, double? durationSeconds)
    {
        try
        {
            if (playback.Disposed)
            {
                return;
            }

            SetProperty(playback.Controls, "currentPosition", startSeconds);
            InvokeMethod(playback.Controls, "play");
            double stopAfterSeconds = durationSeconds ?? ResolveRemainingDurationSeconds(playback.Player, startSeconds);
            playback.StopTimer = new Timer(
                _ => ReleasePlayback(playback),
                null,
                dueTime: Math.Max((int)Math.Round(stopAfterSeconds * 1000d) + 120, 300),
                period: Timeout.Infinite);
        }
        catch (Exception ex)
        {
            LogPlaybackFailure(ex);
            ReleasePlayback(playback);
            TryPlayWindowsBeep(WindowsWarningBeep);
        }
    }

    [SupportedOSPlatform("windows")]
    private static double ResolveRemainingDurationSeconds(object player, double startSeconds)
    {
        try
        {
            object currentMedia = GetProperty(player, "currentMedia");
            object? durationValue = GetProperty(currentMedia, "duration");
            if (durationValue is double totalDuration && totalDuration > 0d)
            {
                return Math.Max(totalDuration - Math.Max(startSeconds, 0d), 0.25d);
            }
        }
        catch
        {
        }

        return 1.5d;
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterPlayback(MediaPlayback playback)
    {
        lock (PlaybackSync)
        {
            ActivePlaybacks.Add(playback);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void ReleasePlayback(MediaPlayback playback)
    {
        lock (PlaybackSync)
        {
            ActivePlaybacks.Remove(playback);
        }

        playback.Dispose();
    }

    private static object GetProperty(object instance, string propertyName)
    {
        return instance.GetType().InvokeMember(
            propertyName,
            BindingFlags.GetProperty,
            binder: null,
            target: instance,
            args: null)
            ?? throw new InvalidOperationException($"Property '{propertyName}' returned null.");
    }

    private static void SetProperty(object instance, string propertyName, object value)
    {
        instance.GetType().InvokeMember(
            propertyName,
            BindingFlags.SetProperty,
            binder: null,
            target: instance,
            args: [value]);
    }

    private static object? InvokeMethod(object instance, string methodName, params object[] arguments)
    {
        return instance.GetType().InvokeMember(
            methodName,
            BindingFlags.InvokeMethod,
            binder: null,
            target: instance,
            args: arguments);
    }

    private static void TryPlayWindowsBeep(uint kind)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            MessageBeep(kind);
        }
        catch
        {
        }
    }

    private static void LogPlaybackFailure(Exception exception)
    {
        try
        {
            string logPath = Path.Combine(AppContext.BaseDirectory, "sound-error.log");
            string content =
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] Notification sound playback failure{Environment.NewLine}" +
                exception +
                Environment.NewLine +
                Environment.NewLine;
            File.AppendAllText(logPath, content);
        }
        catch
        {
        }
    }

    [SupportedOSPlatform("windows")]
    private sealed class MediaPlayback(object player, object controls) : IDisposable
    {
        private bool _disposed;

        public object Player { get; } = player;

        public object Controls { get; } = controls;

        public Timer? StartTimer { get; set; }

        public Timer? StopTimer { get; set; }

        public bool Disposed => _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StartTimer?.Dispose();
            StopTimer?.Dispose();

            try
            {
                InvokeMethod(Controls, "stop");
            }
            catch
            {
            }

            try
            {
                InvokeMethod(Player, "close");
            }
            catch
            {
            }

            try
            {
                Marshal.FinalReleaseComObject(Controls);
            }
            catch
            {
            }

            try
            {
                Marshal.FinalReleaseComObject(Player);
            }
            catch
            {
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint type);

    private readonly record struct SoundClip(string FileName, double StartSeconds, double? DurationSeconds);
}
