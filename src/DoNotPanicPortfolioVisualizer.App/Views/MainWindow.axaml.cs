// ============================================================================
// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Proprietary rights reserved except as expressly licensed herein.
//
// DO NOT PANIC PORTFOLIO VISUALIZER
// This file is governed by the SANYALnet Labs Non-Commercial License in the
// root LICENSE file. Non-Commercial use is permitted; Commercial Use and use
// for AI/ML model training are prohibited unless separately authorized.
//
// Attribution is required: "Based on original work by Supratim Sanyal of
// SANYALnet Labs." See LICENSE for full terms, warranty disclaimer, termination,
// patent, trademark, and governing-law provisions.
// ============================================================================
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Media.Imaging;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;

namespace DoNotPanicPortfolioVisualizer.App.Views;

public partial class MainWindow : Window
{
    private bool _dataContextDisposed;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Screen? screen = Screens.ScreenFromWindow(this);
        if (screen is null)
            return;

        // Screen work areas are physical pixels.  Keep the configuration dialog on the
        // owner's monitor and leave its desktop chrome unobscured at every DPI scale.
        double scale = Math.Max(1d, RenderScaling);
        double availableWidth = Math.Max(1d, (screen.WorkingArea.Width - 24d) / scale);
        double availableHeight = Math.Max(1d, (screen.WorkingArea.Height - 24d) / scale);
        MaxWidth = availableWidth;
        MaxHeight = availableHeight;
        MinWidth = Math.Min(MinWidth, availableWidth);
        MinHeight = Math.Min(MinHeight, availableHeight);
        Width = Math.Min(Width, availableWidth);
        Height = Math.Min(Height, availableHeight);

        string? capturePath = Environment.GetEnvironmentVariable("DNPPV_CONFIG_CAPTURE_PATH");
        if (!string.IsNullOrWhiteSpace(capturePath))
            _ = CaptureValidationWindowAsync(capturePath);
    }

    private async Task CaptureValidationWindowAsync(string capturePath)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(true);
            if (!IsVisible || ClientSize.Width <= 0 || ClientSize.Height <= 0)
                return;

            string? directory = Path.GetDirectoryName(capturePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            double scale = Math.Max(1d, RenderScaling);
            PixelSize pixelSize = new(
                (int)Math.Ceiling(ClientSize.Width * scale),
                (int)Math.Ceiling(ClientSize.Height * scale));
            using RenderTargetBitmap bitmap = new(pixelSize, new Vector(96d * scale, 96d * scale));
            bitmap.Render(this);
            bitmap.Save(capturePath, PngBitmapEncoderOptions.Default);
            TraceLog.InfoState("App.Configuration", "ValidationWindowCapture", [
                new("path", capturePath),
                new("width", pixelSize.Width),
                new("height", pixelSize.Height)
            ]);
        }
        catch (Exception ex)
        {
            TraceLog.ErrorState("App.Configuration", "ValidationWindowCaptureFailed", [], ex);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_dataContextDisposed)
            return;

        _dataContextDisposed = true;
        Closed -= OnClosed;
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
    }
}
