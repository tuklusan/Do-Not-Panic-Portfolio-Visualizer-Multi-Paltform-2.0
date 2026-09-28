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
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DoNotPanicPortfolioVisualizer.Core;

namespace DoNotPanicPortfolioVisualizer.App.Views;

internal sealed class DuplicateInstanceWindow : Window
{
    private readonly DispatcherTimer _closeTimer;

    public DuplicateInstanceWindow()
    {
        Title = AppIdentity.ProductDisplayName;
        Width = 440;
        Height = 180;
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Button okButton = new()
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Center,
            MinWidth = 88,
        };
        okButton.Click += (_, _) => Close();

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 20,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "Sorry, DO NOT PANIC 2.0 is already active.",
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                },
                okButton,
            },
        };

        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _closeTimer.Tick += (_, _) => Close();
        Opened += (_, _) => _closeTimer.Start();
        Closed += (_, _) => _closeTimer.Stop();
    }
}
