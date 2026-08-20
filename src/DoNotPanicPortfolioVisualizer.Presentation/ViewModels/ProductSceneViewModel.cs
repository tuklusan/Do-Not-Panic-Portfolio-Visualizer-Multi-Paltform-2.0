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
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Data.Interfaces;
using DoNotPanicPortfolioVisualizer.Data.Providers;
using DoNotPanicPortfolioVisualizer.Data.Runtime;
using DoNotPanicPortfolioVisualizer.Data.Services;
using DoNotPanicPortfolioVisualizer.Render.Services;
using DoNotPanicPortfolioVisualizer.Render.ViewModels;
using DoNotPanicPortfolioVisualizer.Shared.Services;

namespace DoNotPanicPortfolioVisualizer.Presentation.ViewModels;

public sealed partial class ProductSceneViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly (string Label, string Symbol)[] MacroSymbols =
    [
        ("VIX", "^VIX"),
        ("NASDAQ", "^IXIC"),
        ("US10Y", "^TNX"),
        ("GOLD", "GC=F"),
        ("CRUDE", "CL=F"),
        ("DXY", "DX-Y.NYB"),
        ("BTC", "BTC-USD")
    ];

    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly SettingsFileService _settingsService;
    private readonly IQuoteProvider _quoteProvider;
    private readonly YFinanceProtocolRuntimeClient _protocolClient;
    private readonly YFinanceServerProcessManager _serverManager;
    private Task? _refreshLoop;

    [ObservableProperty]
    private string _marketStatusText = "Market: New York -- loading";

    [ObservableProperty]
    private string _lastUpdatedText = "Last Updated: waiting for live quote";

    [ObservableProperty]
    private string _dataFreshnessText = "LOADING - initializing local market service";

    [ObservableProperty]
    private string _freshnessBrush = "#D4DEE5";

    [ObservableProperty]
    private string _clockDateText = DateTimeOffset.Now.ToString("ddd dd-MMM-yyyy").ToUpperInvariant();

    [ObservableProperty]
    private string _clockText = DateTimeOffset.UtcNow.ToString("HH:mm:ss 'UTC'");

    private ProductSceneViewModel(
        SettingsFileService settingsService,
        IQuoteProvider quoteProvider,
        YFinanceProtocolRuntimeClient protocolClient,
        YFinanceServerProcessManager serverManager)
    {
        _settingsService = settingsService;
        _quoteProvider = quoteProvider;
        _protocolClient = protocolClient;
        _serverManager = serverManager;
        Lanes = [];
        MacroQuotes = new ObservableCollection<MacroQuoteViewModel>(
            MacroSymbols.Select(static item => new MacroQuoteViewModel(item.Label, item.Symbol)));
        LoadSettings();
    }

    public ObservableCollection<TickerLaneViewModel> Lanes { get; }
    public ObservableCollection<MacroQuoteViewModel> MacroQuotes { get; }

    public static ProductSceneViewModel CreateDefault()
    {
        YFinanceServerProcessManager manager = new();
        YFinanceProtocolRuntimeClient protocolClient = new();
        ManagedYFinanceRuntimeClient runtimeClient = new(manager, protocolClient, "DNPPV-2.0-Scene");
        YahooFinanceQuoteProvider quoteProvider = new(runtimeClient, throwOnPartial: false);
        return new ProductSceneViewModel(new SettingsFileService(), quoteProvider, protocolClient, manager);
    }

    public Task InitializeAsync()
    {
        _refreshLoop ??= RunRefreshLoopAsync(_lifetimeCts.Token);
        return Task.CompletedTask;
    }

    private void LoadSettings()
    {
        AppSettings settings = _settingsService.Load();
        Lanes.Clear();
        foreach (TickerGroup group in settings.Groups.Where(static group => group.Enabled).Take(4))
            Lanes.Add(new TickerLaneViewModel(group));
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await RefreshClockAndQuotesAsync(cancellationToken);
            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
        }
    }

    private async Task RefreshClockAndQuotesAsync(CancellationToken cancellationToken)
    {
        ClockDateText = DateTimeOffset.Now.ToString("ddd dd-MMM-yyyy").ToUpperInvariant();
        ClockText = DateTimeOffset.UtcNow.ToString("HH:mm:ss 'UTC'");
        DataFreshnessText = "LIVE DATA - connecting to local YFinance service";
        FreshnessBrush = "#F4C95D";

        List<(string Symbol, Action<QuoteSnapshot> Apply)> targets = [];
        targets.AddRange(MacroQuotes.Select(macro => (macro.Symbol, (Action<QuoteSnapshot>)macro.Apply)));
        targets.AddRange(Lanes.SelectMany(static lane => lane.Quotes)
            .Select(ticker => (ticker.Symbol, (Action<QuoteSnapshot>)ticker.Apply)));

        int successCount = 0;
        foreach ((string symbol, Action<QuoteSnapshot> apply) in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                IReadOnlyList<QuoteSnapshot> quotes = await _quoteProvider.GetQuotesAsync([symbol], cancellationToken);
                QuoteSnapshot? quote = quotes.FirstOrDefault();
                if (quote is null)
                    continue;

                apply(quote);
                successCount++;
                LastUpdatedText = "Last Updated: " + TickerFormatter.FormatUpdatedSymbol(quote);
                MarketStatusText = "Market: New York " + FormatMarketSession(quote.MarketSession);
                DataFreshnessText = quote.IsStale ? "DELAYED - cached market data" : "LIVE quote feed";
                FreshnessBrush = quote.IsStale ? "#F4C95D" : "#39E75F";
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                DataFreshnessText = successCount == 0
                    ? "OFFLINE - waiting for local market service"
                    : $"DEGRADED - {successCount} symbols updated";
                FreshnessBrush = "#FF8A55";
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
        }

        if (successCount == 0)
            MarketStatusText = "Market: New York -- unavailable";
    }

    private static string FormatMarketSession(MarketSession session)
        => session switch
        {
            MarketSession.PreMarket => "Pre-Market",
            MarketSession.Regular => "Open",
            MarketSession.AfterHours => "After Hours",
            MarketSession.Closed => "Closed",
            _ => "Status Unknown"
        };

    public async ValueTask DisposeAsync()
    {
        await _lifetimeCts.CancelAsync();
        if (_refreshLoop is not null)
        {
            try
            {
                await _refreshLoop;
            }
            catch (OperationCanceledException)
            {
            }
        }

        await _serverManager.StopOwnedServerAsync();
        await _protocolClient.DisposeAsync();
        _serverManager.Dispose();
        _lifetimeCts.Dispose();
    }
}
