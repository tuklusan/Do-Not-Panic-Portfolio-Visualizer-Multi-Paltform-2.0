// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Based on original work by Supratim Sanyal of SANYALnet Labs.
// Governed by the SANYALnet Labs Non-Commercial License in the root LICENSE file.

using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;
using ManagedBass;

namespace DoNotPanicPortfolioVisualizer.App.ViewModels;

internal sealed partial class AudioPlayerViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly StreamEndpoint[] StreamEndpoints =
    [
        new("https://fr.ah.fm/mobile", 64, "AAC"),
        new("https://fr2.ah.fm/mobile", 64, "AAC"),
        new("https://eu.ah.fm/mobile", 64, "AAC"),
        new("https://fr.ah.fm/live", 192, "MP3"),
        new("https://fr2.ah.fm/live", 192, "MP3"),
        new("https://eu.ah.fm/live", 192, "MP3"),
        new("http://fr.ah.fm/live", 192, "MP3")
    ];

    private readonly object _stateLock = new();
    private CancellationTokenSource? _playbackCts;
    private Task _playbackTask = Task.CompletedTask;
    private int _streamHandle;
    private bool _userPaused = true;
    private bool _disposed;
    private bool _bassInitialized;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private string statusText = "Idle";

    [ObservableProperty]
    private float volume = 0.8f;

    private AudioPlayerViewModel()
    {
    }

    public string PlaybackGlyph => IsPlaying ? "Ⅱ" : "▶";

    public string PlaybackToolTip => IsPlaying ? "Pause AH.FM" : "Play AH.FM";

    public static AudioPlayerViewModel? TryCreate()
    {
        try
        {
            BassNativeResolver.Register();

            // BASS measures NetPreBuffer as a percentage of NetBufferLength; milliseconds would have been an indecently simple arrangement.
            Bass.NetBufferLength = 6000;
            Bass.NetPreBuffer = 75;
            Bass.NetTimeOut = 8000;
            Bass.NetReadTimeOut = 15000;
            Bass.PlaybackBufferLength = 750;

            AudioPlayerViewModel player = new();
            if (!Bass.Init(-1, 44100, DeviceInitFlags.Default))
            {
                TraceLog.ErrorState("RadioPlayer", "AudioDeviceInitializationFailed", [
                    new("error", Bass.LastError.ToString())
                ]);
                return null;
            }

            player._bassInitialized = true;
            return player;
        }
        catch (Exception exception)
        {
            TraceLog.ErrorState("RadioPlayer", "AudioDeviceInitializationFailed", [], exception);
            return null;
        }
    }

    public void StartAutomatically()
    {
        lock (_stateLock)
        {
            _userPaused = false;
            if (_disposed || _playbackCts is not null)
                return;

            CancellationTokenSource source = new();
            _playbackCts = source;
            _playbackTask = Task.Run(() => RunPlaybackLoopAsync(source));
        }
    }

    [RelayCommand]
    private void TogglePlayback()
    {
        lock (_stateLock)
        {
            if (_disposed)
                return;

            if (!_userPaused)
            {
                _userPaused = true;
                _playbackCts?.Cancel();
                SetStatus("Stopping...");
                return;
            }
        }

        StartAutomatically();
    }

    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlaybackGlyph));
        OnPropertyChanged(nameof(PlaybackToolTip));
    }

    partial void OnVolumeChanged(float value)
    {
        lock (_stateLock)
        {
            if (_streamHandle != 0)
                Bass.ChannelSetAttribute(_streamHandle, ChannelAttribute.Volume, value);
        }
    }

    private async Task RunPlaybackLoopAsync(CancellationTokenSource source)
    {
        CancellationToken cancellationToken = source.Token;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                bool streamStarted = false;
                foreach (StreamEndpoint endpoint in GetSupportedEndpoints())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    SetStatus($"Connecting ({endpoint.BitrateKbps} kbps {endpoint.Format})...");

                    int handle;
                    Errors error;
                    try
                    {
                        (handle, error) = await Task.Run(() =>
                        {
                            int stream = Bass.CreateStream(
                                endpoint.Url,
                                0,
                                // Live radio has nowhere sensible to seek; block streaming keeps the network buffer bounded by NetBufferLength while the music wanders on.
                                BassFlags.Default | BassFlags.StreamDownloadBlocks,
                                null,
                                IntPtr.Zero);
                            return (stream, stream == 0 ? Bass.LastError : Errors.OK);
                        }).ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        TraceAttemptFailed(endpoint, exception.Message);
                        continue;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        if (handle != 0)
                            Bass.StreamFree(handle);
                        break;
                    }

                    if (handle == 0)
                    {
                        TraceAttemptFailed(endpoint, error.ToString());
                        continue;
                    }

                    lock (_stateLock)
                    {
                        if (_disposed || cancellationToken.IsCancellationRequested)
                        {
                            Bass.StreamFree(handle);
                            break;
                        }

                        _streamHandle = handle;
                        Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, Volume);
                    }

                    if (!Bass.ChannelPlay(handle))
                    {
                        Errors playError = Bass.LastError;
                        ReleaseStream(handle);
                        TraceAttemptFailed(endpoint, playError.ToString());
                        continue;
                    }

                    SetPlaybackState(true, $"Streaming ({endpoint.BitrateKbps} kbps {endpoint.Format})");
                    TraceLog.InfoState("RadioPlayer", "StreamPlaybackStarted", [
                        new("relay", GetRelayHost(endpoint.Url)),
                        new("bitrateKbps", endpoint.BitrateKbps),
                        new("format", endpoint.Format),
                        new("networkBufferMs", Bass.NetBufferLength),
                        new("preBufferPercent", Bass.NetPreBuffer)
                    ]);

                    bool endpointWorked = await MonitorStreamAsync(handle, endpoint, cancellationToken).ConfigureAwait(false);
                    ReleaseStream(handle);
                    SetPlaybackState(false, endpointWorked ? "Reconnecting..." : "Trying the next stream...");
                    if (!endpointWorked)
                        continue;

                    streamStarted = true;
                    break;
                }

                if (cancellationToken.IsCancellationRequested)
                    break;

                if (!streamStarted)
                    SetStatus("All audio streams failed; retrying...");

                await Task.Delay(streamStarted ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(10), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            TraceLog.ErrorState("RadioPlayer", "PlaybackLoopFailed", [], exception);
            SetStatus("Playback stopped; retrying...");
        }
        finally
        {
            int handle;
            lock (_stateLock)
            {
                handle = _streamHandle;
                _streamHandle = 0;
            }

            if (handle != 0)
                FreeStream(handle);

            SetPlaybackState(false, _disposed ? "Stopped" : "Paused");
            lock (_stateLock)
            {
                if (ReferenceEquals(_playbackCts, source))
                {
                    _playbackCts = null;
                    source.Dispose();
                }

                if (!_disposed && !_userPaused)
                    StartAutomatically();
            }
        }
    }

    private async Task<bool> MonitorStreamAsync(
        int handle,
        StreamEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        DateTimeOffset? stalledAt = null;
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        bool hasPlayed = false;

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
            PlaybackState state = Bass.ChannelIsActive(handle);

            if (state == PlaybackState.Playing)
            {
                if (!hasPlayed || stalledAt is not null)
                    SetPlaybackState(true, $"Streaming ({endpoint.BitrateKbps} kbps {endpoint.Format})");

                hasPlayed = true;
                stalledAt = null;
                continue;
            }

            if (state == PlaybackState.Stalled)
            {
                stalledAt ??= DateTimeOffset.UtcNow;
                SetPlaybackState(false, "Buffering...");
                if (DateTimeOffset.UtcNow - stalledAt < TimeSpan.FromSeconds(20))
                    continue;

                TraceLog.WarnState("RadioPlayer", "StreamStallReconnect", [
                    new("relay", GetRelayHost(endpoint.Url)),
                    new("stalledSeconds", Math.Round((DateTimeOffset.UtcNow - stalledAt.Value).TotalSeconds, 1))
                ]);
                return hasPlayed;
            }

            if (DateTimeOffset.UtcNow - startedAt < TimeSpan.FromSeconds(3))
            {
                // If a stream stops during its first three seconds, the endpoint has probably arrived speaking a codec nobody here understands.
                TraceAttemptFailed(endpoint, $"stream stopped during startup ({state})");
                return false;
            }

            TraceLog.WarnState("RadioPlayer", "StreamStoppedReconnect", [
                new("relay", GetRelayHost(endpoint.Url)),
                new("state", state.ToString())
            ]);
            return hasPlayed;
        }

        return hasPlayed;
    }

    private static IEnumerable<StreamEndpoint> GetSupportedEndpoints()
    {
        // The bundled BASS core borrows AAC decoding from Windows and macOS; Linux needs the separate GPL BASS_AAC add-on.
        // To avoid that licensing escapade, Linux skips AAC and starts with the lowest-bitrate format its bundled decoder can play.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return StreamEndpoints.Where(endpoint => endpoint.Format != "AAC");

        return StreamEndpoints;
    }

    private void TraceAttemptFailed(StreamEndpoint endpoint, string error)
    {
        TraceLog.WarnState("RadioPlayer", "StreamAttemptFailed", [
            new("relay", GetRelayHost(endpoint.Url)),
            new("bitrateKbps", endpoint.BitrateKbps),
            new("format", endpoint.Format),
            new("error", error)
        ]);
    }

    private static string GetRelayHost(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.Host : "unknown";

    private void ReleaseStream(int handle)
    {
        lock (_stateLock)
        {
            if (_streamHandle == handle)
                _streamHandle = 0;
        }

        FreeStream(handle);
    }

    private static void FreeStream(int handle)
    {
        try
        {
            Bass.ChannelStop(handle);
            Bass.StreamFree(handle);
        }
        catch
        {
        }
    }

    private void SetStatus(string status)
        => Dispatcher.UIThread.Post(() => StatusText = status);

    private void SetPlaybackState(bool isPlaying, string status)
        => Dispatcher.UIThread.Post(() =>
        {
            IsPlaying = isPlaying;
            StatusText = status;
        });

    public async ValueTask DisposeAsync()
    {
        Task playbackTask;
        lock (_stateLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _userPaused = true;
            _playbackCts?.Cancel();
            playbackTask = _playbackTask;
        }

        try
        {
            await playbackTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        if (_bassInitialized)
        {
            Bass.Free();
            _bassInitialized = false;
        }
    }

    private sealed record StreamEndpoint(string Url, int BitrateKbps, string Format);

    private static class BassNativeResolver
    {
        private static int _registered;

        public static void Register()
        {
            if (Interlocked.Exchange(ref _registered, 1) != 0)
                return;

            try
            {
                NativeLibrary.SetDllImportResolver(typeof(Bass).Assembly, Resolve);
            }
            catch
            {
                Volatile.Write(ref _registered, 0);
                throw;
            }
        }

        private static IntPtr Resolve(
            string libraryName,
            Assembly assembly,
            DllImportSearchPath? searchPath)
        {
            if (!string.Equals(Path.GetFileNameWithoutExtension(libraryName), "bass", StringComparison.OrdinalIgnoreCase))
                return IntPtr.Zero;

            string nativeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "bass.dll"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? "libbass.dylib"
                    : "libbass.so";
            string libraryPath = Path.Combine(
                AppContext.BaseDirectory,
                "runtimes",
                RuntimeInformation.RuntimeIdentifier,
                "native",
                nativeName);

            return File.Exists(libraryPath)
                ? NativeLibrary.Load(libraryPath)
                : IntPtr.Zero;
        }
    }
}
