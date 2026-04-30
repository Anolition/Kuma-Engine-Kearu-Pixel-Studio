using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ProjectSPlus.Core.Configuration;

namespace ProjectSPlus.App;

internal static class BackgroundMusicPlayer
{
    public const string DefaultTrackFileName = "telescope-lilac.mp3";
    public const int DefaultVolumePercent = 16;
    private const double SoftLoopFadeSeconds = 2.8d;
    private const int SoftLoopFadeSteps = 18;

    private static readonly string[] SupportedExtensions = [".mp3", ".wav", ".wma"];
    private static readonly object PlaybackSync = new();
    private static MediaLoopPlayback? _playback;
    private static string? _currentPath;
    private static int _currentVolumePercent = DefaultVolumePercent;
    private static BackgroundMusicPlaybackMode _currentPlaybackMode = BackgroundMusicPlaybackMode.LoopTrack;

    public static bool IsAvailable => OperatingSystem.IsWindows();

    public static string? CurrentTrackFileName
    {
        get
        {
            lock (PlaybackSync)
            {
                return string.IsNullOrWhiteSpace(_currentPath)
                    ? null
                    : Path.GetFileName(_currentPath);
            }
        }
    }

    public static IReadOnlyList<string> GetAvailableTrackFileNames()
    {
        string musicDirectory = GetMusicDirectory();
        if (!Directory.Exists(musicDirectory))
        {
            return [];
        }

        return Directory.GetFiles(musicDirectory)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
            .Cast<string>()
            .OrderByDescending(IsPreferredTelescopeLilacName)
            .ThenBy(fileName => fileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string GetTrackLabel(string? fileName)
    {
        string resolved = ResolveTrackFileName(fileName) ?? NormalizeRequestedTrackName(fileName);
        string label = CleanTrackLabel(Path.GetFileNameWithoutExtension(resolved));
        if (string.IsNullOrWhiteSpace(label))
        {
            label = "Telescope Lilac";
        }

        return string.Join(
            " ",
            label.Replace('-', ' ')
                .Replace('_', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(CapitalizeWord));
    }

    public static string? ResolveTrackFileName(string? requestedFileName)
    {
        IReadOnlyList<string> tracks = GetAvailableTrackFileNames();
        if (tracks.Count == 0)
        {
            return null;
        }

        string normalizedRequested = NormalizeTrackKey(NormalizeRequestedTrackName(requestedFileName));
        string? exactMatch = tracks.FirstOrDefault(track => string.Equals(track, requestedFileName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(exactMatch))
        {
            return exactMatch;
        }

        string? normalizedMatch = tracks.FirstOrDefault(track => string.Equals(NormalizeTrackKey(track), normalizedRequested, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(normalizedMatch))
        {
            return normalizedMatch;
        }

        string? telescopeLilac = tracks.FirstOrDefault(IsPreferredTelescopeLilacName);
        return telescopeLilac ?? tracks[0];
    }

    public static string GetNextTrackFileName(string? currentFileName)
    {
        IReadOnlyList<string> tracks = GetAvailableTrackFileNames();
        if (tracks.Count == 0)
        {
            return NormalizeRequestedTrackName(currentFileName);
        }

        string? resolvedCurrent = ResolveTrackFileName(currentFileName);
        int currentIndex = resolvedCurrent is null
            ? -1
            : tracks.Select((track, index) => new { track, index })
                .FirstOrDefault(entry => string.Equals(entry.track, resolvedCurrent, StringComparison.OrdinalIgnoreCase))
                ?.index ?? -1;
        return tracks[(currentIndex + 1 + tracks.Count) % tracks.Count];
    }

    public static void Apply(bool enabled, string? requestedFileName, int volumePercent, BackgroundMusicPlaybackMode playbackMode = BackgroundMusicPlaybackMode.LoopTrack)
    {
        int resolvedVolume = NormalizeVolume(volumePercent);
        if (!enabled || !OperatingSystem.IsWindows())
        {
            Stop();
            return;
        }

        string? resolvedFileName = ResolveTrackFileName(requestedFileName);
        if (string.IsNullOrWhiteSpace(resolvedFileName))
        {
            Stop();
            return;
        }

        string trackPath = Path.Combine(GetMusicDirectory(), resolvedFileName);
        if (!File.Exists(trackPath))
        {
            Stop();
            return;
        }

#pragma warning disable CA1416
        StartOrUpdate(trackPath, resolvedVolume, playbackMode);
#pragma warning restore CA1416
    }

    public static void SetVolume(int volumePercent)
    {
        int resolvedVolume = NormalizeVolume(volumePercent);
        lock (PlaybackSync)
        {
            _currentVolumePercent = resolvedVolume;
            _playback?.SetVolume(resolvedVolume);
        }
    }

    public static void Stop()
    {
        lock (PlaybackSync)
        {
            _playback?.Dispose();
            _playback = null;
            _currentPath = null;
            _currentPlaybackMode = BackgroundMusicPlaybackMode.LoopTrack;
        }
    }

    private static string NormalizeRequestedTrackName(string? fileName)
    {
        return string.IsNullOrWhiteSpace(fileName)
            ? DefaultTrackFileName
            : Path.GetFileName(fileName);
    }

    private static int NormalizeVolume(int volumePercent)
    {
        return Math.Clamp(volumePercent, 0, 100);
    }

    [SupportedOSPlatform("windows")]
    private static void StartOrUpdate(string trackPath, int volumePercent, BackgroundMusicPlaybackMode playbackMode)
    {
        lock (PlaybackSync)
        {
            if (_playback is not null && string.Equals(_currentPath, trackPath, StringComparison.OrdinalIgnoreCase))
            {
                _currentVolumePercent = volumePercent;
                _currentPlaybackMode = playbackMode;
                _playback.PlaybackMode = playbackMode;
                _playback.SetVolume(volumePercent);
                return;
            }

            _playback?.Dispose();
            _playback = null;
            _currentPath = null;

            try
            {
                Type? playerType = Type.GetTypeFromProgID("WMPlayer.OCX");
                if (playerType is null)
                {
                    return;
                }

                object player = Activator.CreateInstance(playerType)
                    ?? throw new InvalidOperationException("Unable to create WMPlayer.OCX instance.");
                object settings = GetProperty(player, "settings");
                object controls = GetProperty(player, "controls");

                SetProperty(settings, "volume", volumePercent);
                InvokeMethod(settings, "setMode", "loop", true);
                SetProperty(player, "URL", trackPath);

                MediaLoopPlayback playback = new(player, settings, controls, playbackMode);
                playback.SetVolume(volumePercent);
                playback.PlayTimer = new Timer(
                    _ => playback.Play(),
                    null,
                    dueTime: 160,
                    period: Timeout.Infinite);

                _playback = playback;
                _currentPath = trackPath;
                _currentPlaybackMode = playbackMode;
                _currentVolumePercent = volumePercent;
            }
            catch (Exception ex)
            {
                LogPlaybackFailure(ex);
                _playback?.Dispose();
                _playback = null;
                _currentPath = null;
            }
        }
    }

    private static void AdvanceToNextPlaylistTrack()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        lock (PlaybackSync)
        {
            if (_playback is null || _currentPath is null)
            {
                return;
            }

            string currentFileName = Path.GetFileName(_currentPath);
            string nextFileName = GetNextTrackFileName(currentFileName);
            string nextPath = Path.Combine(GetMusicDirectory(), nextFileName);
            if (!File.Exists(nextPath))
            {
                return;
            }

#pragma warning disable CA1416
            StartOrUpdate(nextPath, _currentVolumePercent, _currentPlaybackMode);
#pragma warning restore CA1416
        }
    }

    private static string GetMusicDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Music");
    }

    private static bool IsPreferredTelescopeLilacName(string fileName)
    {
        return NormalizeTrackKey(fileName).Contains("telescopelilac", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeTrackKey(string? fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
        return new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string CapitalizeWord(string word)
    {
        return string.IsNullOrWhiteSpace(word)
            ? string.Empty
            : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();
    }

    private static string CleanTrackLabel(string label)
    {
        string cleaned = label;
        int mainVersionIndex = cleaned.IndexOf("-main-version", StringComparison.OrdinalIgnoreCase);
        if (mainVersionIndex >= 0)
        {
            cleaned = cleaned[..mainVersionIndex];
        }

        string[] knownArtistSuffixes =
        [
            "-kevin-macleod",
            "-danijel-zambo"
        ];
        foreach (string suffix in knownArtistSuffixes)
        {
            if (cleaned.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[..^suffix.Length];
                break;
            }
        }

        return cleaned;
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

    private static double ResolveDurationSeconds(object player)
    {
        try
        {
            object currentMedia = GetProperty(player, "currentMedia");
            object? durationValue = GetProperty(currentMedia, "duration");
            if (durationValue is double duration && duration > SoftLoopFadeSeconds + 1d)
            {
                return duration;
            }
        }
        catch
        {
        }

        return 0d;
    }

    private static void LogPlaybackFailure(Exception exception)
    {
        try
        {
            string logPath = Path.Combine(AppContext.BaseDirectory, "sound-error.log");
            string content =
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] Background music playback failure{Environment.NewLine}" +
                exception +
                Environment.NewLine +
                Environment.NewLine;
            File.AppendAllText(logPath, content);
        }
        catch
        {
        }
    }

    private sealed class MediaLoopPlayback(object player, object settings, object controls, BackgroundMusicPlaybackMode playbackMode) : IDisposable
    {
        private const int MaxDurationScheduleAttempts = 30;

        private readonly object _sync = new();
        private bool _disposed;
        private int _targetVolumePercent = DefaultVolumePercent;
        private int _durationScheduleAttempts;

        public BackgroundMusicPlaybackMode PlaybackMode { get; set; } = playbackMode;

        public object Player { get; } = player;

        public object Settings { get; } = settings;

        public object Controls { get; } = controls;

        public Timer? PlayTimer { get; set; }

        public Timer? LoopTimer { get; set; }

        public Timer? FadeTimer { get; set; }

        public void SetVolume(int volumePercent)
        {
            lock (_sync)
            {
                _targetVolumePercent = Math.Clamp(volumePercent, 0, 100);
                if (_disposed)
                {
                    return;
                }

                TrySetVolume(_targetVolumePercent);
            }
        }

        public void Play()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                InvokeMethod(Controls, "play");
                ScheduleSoftLoop();
            }
            catch (Exception ex)
            {
                LogPlaybackFailure(ex);
            }
        }

        private void ScheduleSoftLoop()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                LoopTimer?.Dispose();
                double durationSeconds = ResolveDurationSeconds(Player);
                if (durationSeconds <= SoftLoopFadeSeconds + 1d)
                {
                    if (_durationScheduleAttempts++ < MaxDurationScheduleAttempts)
                    {
                        LoopTimer = new Timer(
                            _ => ScheduleSoftLoop(),
                            null,
                            dueTime: 1000,
                            period: Timeout.Infinite);
                    }

                    return;
                }

                _durationScheduleAttempts = 0;
                int dueTimeMilliseconds = Math.Max((int)Math.Round((durationSeconds - SoftLoopFadeSeconds) * 1000d), 1000);
                LoopTimer = new Timer(
                    _ => BeginSoftLoopRestart(),
                    null,
                    dueTime: dueTimeMilliseconds,
                    period: Timeout.Infinite);
            }
        }

        private void BeginSoftLoopRestart()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                LoopTimer?.Dispose();
                LoopTimer = null;
                if (PlaybackMode == BackgroundMusicPlaybackMode.Playlist)
                {
                    StartFade(_targetVolumePercent, 0, SoftLoopFadeSteps, AdvanceToNextPlaylistTrack);
                    return;
                }

                StartFade(_targetVolumePercent, 0, SoftLoopFadeSteps, RestartAtBeginningAndFadeIn);
            }
        }

        private void RestartAtBeginningAndFadeIn()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    SetProperty(Controls, "currentPosition", 0d);
                    TrySetVolume(0);
                    InvokeMethod(Controls, "play");
                }
                catch (Exception ex)
                {
                    LogPlaybackFailure(ex);
                    return;
                }

                StartFade(0, _targetVolumePercent, SoftLoopFadeSteps, ScheduleSoftLoop);
            }
        }

        private void StartFade(int fromVolume, int toVolume, int steps, Action? onComplete)
        {
            FadeTimer?.Dispose();
            int currentStep = 0;
            int intervalMilliseconds = Math.Max((int)Math.Round((SoftLoopFadeSeconds * 1000d) / Math.Max(steps, 1)), 50);
            FadeTimer = new Timer(
                _ =>
                {
                    lock (_sync)
                    {
                        if (_disposed)
                        {
                            return;
                        }

                        currentStep++;
                        float t = Math.Clamp(currentStep / (float)Math.Max(steps, 1), 0f, 1f);
                        int volume = (int)Math.Round(fromVolume + ((toVolume - fromVolume) * t));
                        TrySetVolume(volume);
                        if (currentStep < steps)
                        {
                            return;
                        }

                        FadeTimer?.Dispose();
                        FadeTimer = null;
                        onComplete?.Invoke();
                    }
                },
                null,
                dueTime: 0,
                period: intervalMilliseconds);
        }

        private void TrySetVolume(int volumePercent)
        {
            try
            {
                BackgroundMusicPlayer.SetProperty(Settings, "volume", Math.Clamp(volumePercent, 0, 100));
            }
            catch
            {
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                PlayTimer?.Dispose();
                LoopTimer?.Dispose();
                FadeTimer?.Dispose();
            }

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
                ReleaseComObjectIfWindows(Controls);
            }
            catch
            {
            }

            try
            {
                ReleaseComObjectIfWindows(Settings);
            }
            catch
            {
            }

            try
            {
                ReleaseComObjectIfWindows(Player);
            }
            catch
            {
            }
        }
    }

    private static void ReleaseComObjectIfWindows(object instance)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

#pragma warning disable CA1416
        Marshal.FinalReleaseComObject(instance);
#pragma warning restore CA1416
    }
}
