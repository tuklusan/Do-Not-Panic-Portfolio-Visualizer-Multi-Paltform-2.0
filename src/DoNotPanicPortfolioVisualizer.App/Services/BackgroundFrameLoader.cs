// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Based on original work by Supratim Sanyal of SANYALnet Labs.
// Governed by the SANYALnet Labs Non-Commercial License in the root LICENSE file.

using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using DoNotPanicPortfolioVisualizer.Presentation.Services;

namespace DoNotPanicPortfolioVisualizer.App.Services;

public sealed class BackgroundFrameLoader : IDisposable
{
    private readonly Dictionary<string, BackgroundFrame> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Task<BackgroundFrame> LoadAsync(string source, CancellationToken cancellationToken)
        => Task.Run(() => Load(source, cancellationToken), cancellationToken);

    public void Dispose()
    {
        foreach (BackgroundFrame frame in _cache.Values)
            frame.Bitmap.Dispose();
        _cache.Clear();
    }

    private BackgroundFrame Load(string source, CancellationToken cancellationToken)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(source, out BackgroundFrame? cached))
                return cached;
        }

        cancellationToken.ThrowIfCancellationRequested();
        using Stream bitmapStream = OpenSourceStream(source);
        Bitmap bitmap = new(bitmapStream);
        cancellationToken.ThrowIfCancellationRequested();

        double opacity;
        using (Stream sampleStream = OpenSourceStream(source))
        using (WriteableBitmap sample = WriteableBitmap.DecodeToWidth(
                   sampleStream,
                   48,
                   BitmapInterpolationMode.LowQuality))
        using (ILockedFramebuffer framebuffer = sample.Lock())
        {
            if (framebuffer.Format != PixelFormat.Bgra8888)
            {
                opacity = BackgroundPresentationOpacityPolicy.FallbackOpacity;
            }
            else
            {
                byte[] pixels = new byte[framebuffer.RowBytes * framebuffer.Size.Height];
                Marshal.Copy(framebuffer.Address, pixels, 0, pixels.Length);
                opacity = BackgroundPresentationOpacityPolicy.FromBgra32(
                    pixels,
                    framebuffer.Size.Width,
                    framebuffer.Size.Height,
                    framebuffer.RowBytes);
            }
        }

        BackgroundFrame loaded = new(source, bitmap, opacity);
        lock (_cache)
        {
            if (_cache.TryGetValue(source, out BackgroundFrame? raced))
            {
                bitmap.Dispose();
                return raced;
            }

            _cache.Add(source, loaded);
            return loaded;
        }
    }

    private static Stream OpenSourceStream(string source)
    {
        if (source.StartsWith('/', StringComparison.Ordinal))
            return AssetLoader.Open(new Uri("avares://DoNotPanicPortfolioVisualizer.App" + source));
        return File.OpenRead(source);
    }
}

public sealed record BackgroundFrame(string Source, Bitmap Bitmap, double PresentationOpacity);
