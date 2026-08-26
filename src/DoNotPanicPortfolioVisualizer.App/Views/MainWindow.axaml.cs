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
