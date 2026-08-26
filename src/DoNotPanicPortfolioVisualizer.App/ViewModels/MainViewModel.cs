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
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using DoNotPanicPortfolioVisualizer.Core.Constants;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Core.Services;
using DoNotPanicPortfolioVisualizer.Core.Validation;
using DoNotPanicPortfolioVisualizer.Data.Runtime;
using DoNotPanicPortfolioVisualizer.Data.Services;
using DoNotPanicPortfolioVisualizer.Shared;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;
using DoNotPanicPortfolioVisualizer.Shared.Services;

namespace DoNotPanicPortfolioVisualizer.App.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsFileService _settingsFileService;
    private readonly SettingsValidator _settingsValidator;
    private readonly NewsFeedValidationService _newsFeedValidationService;
    private readonly AiNewsAccessValidationService _aiNewsAccessValidationService;
    private readonly IConnectivityService _connectivityService;
    private readonly bool _ownsConnectivityService;
    private readonly SemaphoreSlim _validationGate = new(1, 1);
    private readonly object _validationCancellationGate = new();
    private CancellationTokenSource? _validationCancellation;
    private AppSettings _loadedSettings;
    private AppSettings? _validatedSettings;
    private IReadOnlyList<QuoteSnapshot> _validatedQuoteSeeds = [];
    private bool _closeAfterValidationCancellation;
    private int _closeRequested;
    private bool _isBusy;
    private bool _isValidated;
    private bool _isLoadingState;
    private bool _isNetworkAvailable;
    private int _connectivityRefreshRunning;
    private bool _disposed;
    private string _statusMessage;
    private string _validationSummary;
    private string _validationLogText;
    private bool _useCustomBackgroundImageFolder;
    private string _customBackgroundImageFolder;
    private bool _backgroundIncludeSubfolders;
    private int _backgroundChangeSeconds;
    private int _newsRefreshMinutes;
    private string _newsFeedUrl;
    private string _aiApiKey;
    private string _aiEndpointUrl;
    private string _aiModelId;
    private NewsScrollerMode _newsScrollerMode;
    private AiWritingStyle _aiWritingStyle;

    public MainViewModel()
        : this(connectivityService: null)
    {
    }

    internal MainViewModel(IConnectivityService? connectivityService)
    {
        _settingsFileService = new SettingsFileService();
        _settingsValidator = new SettingsValidator();
        _newsFeedValidationService = new NewsFeedValidationService();
        _aiNewsAccessValidationService = new AiNewsAccessValidationService();
        _connectivityService = connectivityService ?? new ConfigConnectivityService();
        _ownsConnectivityService = connectivityService is null;
        _loadedSettings = _settingsFileService.Load();
        _statusMessage = $"{PortfolioVersion.DisplayName} configuration ready";
        _validationSummary = "Validate before saving changes.";
        _validationLogText = string.Empty;
        _customBackgroundImageFolder = string.Empty;
        _newsFeedUrl = Defaults.DefaultNewsFeedUrl;
        _aiApiKey = string.Empty;
        _aiEndpointUrl = Defaults.DefaultAiEndpointUrl;
        _aiModelId = Defaults.DefaultAiModelId;
        Groups = [];
        ValidateCommand = new AsyncRelayCommand(ValidateAsync, () => CanValidate);
        CancelValidationCommand = new RelayCommand(() => { CancelValidation(); }, () => CanCancelValidation);
        SaveCommand = new RelayCommand(Save, () => CanSave);
        CancelCommand = new RelayCommand(Cancel);
        RevertCommand = new RelayCommand(Revert, () => CanRevert);
        AddGroupCommand = new RelayCommand(AddGroup, () => CanAddGroup);
        RetryNetworkCommand = new AsyncRelayCommand(
            RetryConnectivityAsync,
            () => !IsBusy && Volatile.Read(ref _connectivityRefreshRunning) == 0);
        ApplyLoadedSettings(_loadedSettings);
        _connectivityService.ConnectivityChanged += OnConnectivityChanged;
        _ = RefreshConnectivityAsync(forceProbe: false);
    }

    public string ProductTitle => PortfolioVersion.DisplayName;
    public string WindowSubtitle => "Avalonia Configuration Window";
    public string RuntimeLine => ".NET 10 + Avalonia 12.1.1";

    public event Action? CloseRequested;

    public ObservableCollection<TickerGroupEditorViewModel> Groups { get; }

    public IAsyncRelayCommand ValidateCommand { get; }
    public IRelayCommand CancelValidationCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand RevertCommand { get; }
    public IRelayCommand AddGroupCommand { get; }
    public IAsyncRelayCommand RetryNetworkCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
                return;

            OnPropertyChanged(nameof(CanValidate));
            OnPropertyChanged(nameof(CanSave));
            OnPropertyChanged(nameof(CanRevert));
            OnPropertyChanged(nameof(CanAddGroup));
            OnPropertyChanged(nameof(CanCancelValidation));
            RefreshCommandStates();
        }
    }

    public bool IsValidated
    {
        get => _isValidated;
        private set
        {
            if (!SetProperty(ref _isValidated, value))
                return;

            OnPropertyChanged(nameof(CanSave));
            RefreshCommandStates();
        }
    }

    public bool IsNetworkAvailable
    {
        get => _isNetworkAvailable;
        private set
        {
            if (!SetProperty(ref _isNetworkAvailable, value))
                return;

            OnPropertyChanged(nameof(IsConfigActive));
            OnPropertyChanged(nameof(ShowNetworkLockOverlay));
            OnPropertyChanged(nameof(CanValidate));
            RefreshCommandStates();
        }
    }

    public bool IsConfigActive => IsNetworkAvailable && !IsBusy;
    public bool ShowNetworkLockOverlay => !IsNetworkAvailable;
    public bool CanValidate => IsConfigActive;
    public bool CanSave => !IsBusy && IsValidated && _validatedSettings is not null;
    public bool CanRevert => !IsBusy;
    public bool CanAddGroup => !IsBusy && Groups.Count < Defaults.MaxTapeCount;
    public bool CanCancelValidation => IsBusy;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetProperty(ref _validationSummary, value);
    }

    public string ValidationLogText
    {
        get => _validationLogText;
        private set => SetProperty(ref _validationLogText, value);
    }

    public bool UseCustomBackgroundImageFolder
    {
        get => _useCustomBackgroundImageFolder;
        set
        {
            if (!SetProperty(ref _useCustomBackgroundImageFolder, value))
                return;

            InvalidateValidation();
        }
    }

    public string CustomBackgroundImageFolder
    {
        get => _customBackgroundImageFolder;
        set
        {
            if (!SetProperty(ref _customBackgroundImageFolder, value))
                return;

            InvalidateValidation();
        }
    }

    public bool BackgroundIncludeSubfolders
    {
        get => _backgroundIncludeSubfolders;
        set
        {
            if (!SetProperty(ref _backgroundIncludeSubfolders, value))
                return;

            InvalidateValidation();
        }
    }

    public int BackgroundChangeSeconds
    {
        get => _backgroundChangeSeconds;
        set
        {
            if (!SetProperty(ref _backgroundChangeSeconds, value))
                return;

            InvalidateValidation();
        }
    }

    public int NewsRefreshMinutes
    {
        get => _newsRefreshMinutes;
        set
        {
            if (!SetProperty(ref _newsRefreshMinutes, value))
                return;

            InvalidateValidation();
        }
    }

    public string NewsFeedUrl
    {
        get => _newsFeedUrl;
        set
        {
            if (!SetProperty(ref _newsFeedUrl, value))
                return;

            InvalidateValidation();
        }
    }

    public string AiApiKey
    {
        get => _aiApiKey;
        set
        {
            if (!SetProperty(ref _aiApiKey, value))
                return;

            InvalidateValidation();
        }
    }

    public string AiEndpointUrl
    {
        get => _aiEndpointUrl;
        set
        {
            if (!SetProperty(ref _aiEndpointUrl, value))
                return;

            InvalidateValidation();
        }
    }

    public string AiModelId
    {
        get => _aiModelId;
        set
        {
            if (!SetProperty(ref _aiModelId, value))
                return;

            InvalidateValidation();
        }
    }

    public NewsScrollerMode NewsScrollerMode
    {
        get => _newsScrollerMode;
        set
        {
            if (!SetProperty(ref _newsScrollerMode, value))
                return;

            OnPropertyChanged(nameof(IsSummarizedFinancialNewsSelected));
            OnPropertyChanged(nameof(IsRssFeedSelected));
            InvalidateValidation();
        }
    }

    public AiWritingStyle AiWritingStyle
    {
        get => _aiWritingStyle;
        set
        {
            if (!SetProperty(ref _aiWritingStyle, value))
                return;

            OnPropertyChanged(nameof(IsDouglasAdamsStyleSelected));
            OnPropertyChanged(nameof(IsWilliamShakespeareStyleSelected));
            InvalidateValidation();
        }
    }

    public bool IsSummarizedFinancialNewsSelected
    {
        get => NewsScrollerMode == NewsScrollerMode.SummarizedFinancialNews;
        set
        {
            if (!value)
                return;

            NewsScrollerMode = NewsScrollerMode.SummarizedFinancialNews;
        }
    }

    public bool IsRssFeedSelected
    {
        get => NewsScrollerMode == NewsScrollerMode.RssFeed;
        set
        {
            if (!value)
                return;

            NewsScrollerMode = NewsScrollerMode.RssFeed;
        }
    }

    public bool IsDouglasAdamsStyleSelected
    {
        get => AiWritingStyle == AiWritingStyle.DouglasAdams;
        set
        {
            if (!value)
                return;

            AiWritingStyle = AiWritingStyle.DouglasAdams;
        }
    }

    public bool IsWilliamShakespeareStyleSelected
    {
        get => AiWritingStyle == AiWritingStyle.WilliamShakespeare;
        set
        {
            if (!value)
                return;

            AiWritingStyle = AiWritingStyle.WilliamShakespeare;
        }
    }

    private async Task ValidateAsync()
    {
        bool gateHeld = false;
        CancellationTokenSource? cancellation = null;

        try
        {
            if (!IsNetworkAvailable)
            {
                StatusMessage = "Internet connection is required before validation can run.";
                ValidationSummary = "Restore connectivity and select Retry network.";
                return;
            }

            gateHeld = await _validationGate.WaitAsync(0);
            if (!gateHeld)
                return;

            cancellation = new CancellationTokenSource();
            lock (_validationCancellationGate)
                _validationCancellation = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            IsBusy = true;
            _validatedQuoteSeeds = [];
            ValidationLogText = "VALIDATION STARTED";
            ResetTickerValidationStates(SymbolValidationState.Checking, "Validation in progress");

            List<string> errors = [];
            AppSettings candidate = BuildCandidateSettings(errors);
            errors.AddRange(_settingsValidator.Validate(candidate));

            string feedNote = string.Empty;
            bool networkAvailable = errors.Count == 0 &&
                await _connectivityService.IsInternetAvailableAsync(cancellationToken);
            if (errors.Count == 0 && !networkAvailable)
                errors.Add("Internet connection is required before configuration validation can run. Retry when connectivity is restored.");
            if (errors.Count == 0 && candidate.NewsScrollerMode == NewsScrollerMode.RssFeed)
            {
                AppendValidationLog("RSS FEED CHECK...");
                NewsFeedValidationResult feedValidation = await _newsFeedValidationService.ValidateAsync(
                    candidate.NewsFeedUrl,
                    candidate.HttpTimeoutSeconds,
                    networkAvailable,
                    cancellationToken);

                candidate.NewsFeedUrl = feedValidation.ResolvedFeedUrl;
                if (!string.Equals(NewsFeedUrl, candidate.NewsFeedUrl, StringComparison.Ordinal))
                    NewsFeedUrl = candidate.NewsFeedUrl;

                feedNote = string.IsNullOrWhiteSpace(feedValidation.Message)
                    ? "RSS feed check passed."
                    : feedValidation.Message;
                AppendValidationLog(feedValidation.ValidationSkipped ? "RSS FEED CHECK SKIPPED" : "RSS FEED CHECK COMPLETE");
            }
            else if (errors.Count == 0 && candidate.NewsScrollerMode == NewsScrollerMode.SummarizedFinancialNews)
            {
                AppendValidationLog("AI NEWS ACCESS CHECK...");
                AiNewsAccessValidationResult aiValidation = await _aiNewsAccessValidationService.ValidateAsync(
                    candidate,
                    networkAvailable,
                    cancellationToken);
                string aiMessage = SensitiveDataRedactor.RedactSensitivePatterns(aiValidation.Message);
                if (!aiValidation.IsValid)
                    errors.Add(aiMessage);

                feedNote = string.IsNullOrWhiteSpace(aiMessage)
                    ? "AI access check passed."
                    : aiMessage;
                AppendValidationLog(aiValidation.ValidationSkipped ? "AI NEWS ACCESS CHECK SKIPPED" : "AI NEWS ACCESS CHECK COMPLETE");
            }

            YahooSymbolValidationResult? symbolValidation = null;
            if (errors.Count == 0)
            {
                AppendValidationLog("TICKER VALIDATION...");
                symbolValidation = await ValidateSymbolsAsync(candidate, cancellationToken);
                foreach (string invalidSymbol in symbolValidation.InvalidSymbols)
                    errors.Add($"YFinance.NET does not recognize '{invalidSymbol}'.");
            }

            if (errors.Count > 0)
            {
                List<string> safeErrors = errors
                    .Select(SensitiveDataRedactor.RedactSensitivePatterns)
                    .ToList();
                ResetTickerValidationStates(SymbolValidationState.Invalid, "Fix validation issues");
                _validatedSettings = null;
                _validatedQuoteSeeds = [];
                IsValidated = false;
                StatusMessage = safeErrors[0];
                ValidationSummary = string.Join(Environment.NewLine, safeErrors);
                AppendValidationLog("VALIDATION FAILED");
                return;
            }

            MarkTickerValidationStatesFromCandidate(candidate, symbolValidation);
            ApplyProviderDisplayNames(symbolValidation);
            candidate = BuildCandidateSettings(errors);
            _validatedSettings = candidate.Clone();
            _validatedQuoteSeeds = symbolValidation?.ValidatedQuotes.Values.Select(CloneQuote).ToList() ?? [];
            IsValidated = true;
            StatusMessage = "Validation passed. Review the settings and click Save to persist them.";
            ValidationSummary = feedNote;
            AppendValidationLog("VALIDATION PASSED");
            if (_closeAfterValidationCancellation)
            {
                _closeAfterValidationCancellation = false;
                RequestClose();
            }
        }
        catch (OperationCanceledException) when (cancellation?.IsCancellationRequested == true)
        {
            MarkUncheckedTickersAsCancelled();
            _validatedSettings = null;
            _validatedQuoteSeeds = [];
            IsValidated = false;
            StatusMessage = "Validation cancelled.";
            ValidationSummary = "No changed settings were saved.";
            AppendValidationLog("VALIDATION CANCELLED");
            if (_closeAfterValidationCancellation)
            {
                _closeAfterValidationCancellation = false;
                RequestClose();
            }
        }
        catch (Exception exception)
        {
            TraceLog.WarnState(
                "Config.Validation",
                "ValidationServiceUnavailable",
                [new("exception_type", exception.GetType().Name)]);
            ResetTickerValidationStates(SymbolValidationState.Invalid, "Validation unavailable");
            _validatedSettings = null;
            _validatedQuoteSeeds = [];
            IsValidated = false;
            StatusMessage = "Validation could not complete.";
            ValidationSummary = "A configuration validation service was unavailable. Review the settings and try again.";
        }
        finally
        {
            if (gateHeld)
            {
                lock (_validationCancellationGate)
                {
                    if (ReferenceEquals(_validationCancellation, cancellation))
                        _validationCancellation = null;
                }
                cancellation?.Dispose();
                IsBusy = false;
                _validationGate.Release();
            }
        }
    }

    private void Save()
    {
        if (_validatedSettings is null)
            return;

        try
        {
            _settingsFileService.Save(_validatedSettings);
            RuntimeQuoteSeedStore.Publish(_validatedQuoteSeeds);
        }
        catch (Exception exception)
        {
            TraceLog.WarnState(
                "Config.Validation",
                "SettingsApplyUnavailable",
                [new("exception_type", exception.GetType().Name)]);
            _validatedSettings = null;
            _validatedQuoteSeeds = [];
            IsValidated = false;
            StatusMessage = "Settings could not be applied.";
            ValidationSummary = "Review the settings storage and validation services, then try again.";
            return;
        }
        _loadedSettings = _validatedSettings.Clone();
        ApplyLoadedSettings(_loadedSettings);
        _validatedSettings = _loadedSettings.Clone();
        IsValidated = true;
        StatusMessage = $"{PortfolioVersion.Version} settings applied at {DateTime.Now:T}.";
        ValidationSummary = "Settings and validated quote seeds were applied to the product.";
        RequestClose();
    }

    private void Cancel()
    {
        if (IsBusy)
        {
            _closeAfterValidationCancellation = true;
            if (!CancelValidation())
                RequestClose();
            return;
        }

        ApplyLoadedSettings(_loadedSettings);
        StatusMessage = "Configuration cancelled. No changes were applied.";
        ValidationSummary = "The product continues with the previous settings.";
        RequestClose();
    }

    private void Revert()
    {
        ApplyLoadedSettings(_loadedSettings);
        StatusMessage = "Reverted unsaved changes to the last persisted configuration.";
        ValidationSummary = "Validate again before saving new edits.";
    }

    private void AddGroup()
    {
        if (Groups.Count >= Defaults.MaxTapeCount)
            return;

        TickerGroupEditorViewModel group = new(Defaults.CreateEmptyTickerGroup(Groups.Count), RemoveGroup);
        Groups.Add(group);
        HookGroup(group);
        InvalidateValidation();
    }

    private void RemoveGroup(TickerGroupEditorViewModel group)
    {
        UnhookGroup(group);
        Groups.Remove(group);
        InvalidateValidation();
    }

    private AppSettings BuildCandidateSettings(List<string> errors)
    {
        AppSettings candidate = _loadedSettings.Clone();
        candidate.UseCustomBackgroundImageFolder = UseCustomBackgroundImageFolder;
        candidate.CustomBackgroundImageFolder = CustomBackgroundImageFolder.Trim();
        candidate.BackgroundIncludeSubfolders = BackgroundIncludeSubfolders;
        candidate.BackgroundChangeSeconds = BackgroundChangeSeconds;
        candidate.NewsRefreshMinutes = NewsRefreshMinutes;
        candidate.NewsFeedUrl = NewsFeedUrl.Trim();
        candidate.AiApiKey = AiApiKey.Trim();
        candidate.AiEndpointUrl = AiEndpointUrl.Trim();
        candidate.AiModelId = AiModelId.Trim();
        candidate.NewsScrollerMode = NewsScrollerMode;
        candidate.AiWritingStyle = AiWritingStyle;
        candidate.Groups = Groups
            .Take(Defaults.MaxTapeCount)
            .Select((group, index) => group.ToModel(index, errors))
            .ToList();
        return AppSettingsNormalizer.Normalize(candidate);
    }

    private void ApplyLoadedSettings(AppSettings settings)
    {
        _isLoadingState = true;
        try
        {
            ClearTrackedGroups();
            Groups.Clear();

            UseCustomBackgroundImageFolder = settings.UseCustomBackgroundImageFolder;
            CustomBackgroundImageFolder = settings.CustomBackgroundImageFolder;
            BackgroundIncludeSubfolders = settings.BackgroundIncludeSubfolders;
            BackgroundChangeSeconds = settings.BackgroundChangeSeconds;
            NewsRefreshMinutes = settings.NewsRefreshMinutes;
            NewsFeedUrl = settings.NewsFeedUrl;
            AiApiKey = settings.AiApiKey;
            AiEndpointUrl = settings.AiEndpointUrl;
            AiModelId = settings.AiModelId;
            NewsScrollerMode = settings.NewsScrollerMode;
            AiWritingStyle = settings.AiWritingStyle;

            foreach (TickerGroup group in settings.Groups)
            {
                TickerGroupEditorViewModel editor = new(group, RemoveGroup);
                Groups.Add(editor);
                HookGroup(editor);
            }

            if (Groups.Count == 0)
            {
                TickerGroupEditorViewModel editor = new(Defaults.CreateEmptyTickerGroup(0), RemoveGroup);
                Groups.Add(editor);
                HookGroup(editor);
            }

            _validatedSettings = null;
            _validatedQuoteSeeds = [];
            IsValidated = false;
        }
        finally
        {
            _isLoadingState = false;
            RefreshCommandStates();
        }
    }

    private readonly HashSet<TickerGroupEditorViewModel> _trackedGroups = [];
    private readonly HashSet<TickerItemEditorViewModel> _trackedTickers = [];

    private void HookGroup(TickerGroupEditorViewModel group)
    {
        if (!_trackedGroups.Add(group))
            return;

        group.PropertyChanged += OnTrackedEditorChanged;
        group.Tickers.CollectionChanged += OnGroupTickersChanged;
        foreach (TickerItemEditorViewModel ticker in group.Tickers)
            HookTicker(ticker);
    }

    private void UnhookGroup(TickerGroupEditorViewModel group)
    {
        if (!_trackedGroups.Remove(group))
            return;

        group.PropertyChanged -= OnTrackedEditorChanged;
        group.Tickers.CollectionChanged -= OnGroupTickersChanged;
        foreach (TickerItemEditorViewModel ticker in group.Tickers.ToArray())
            UnhookTicker(ticker);
    }

    private void HookTicker(TickerItemEditorViewModel ticker)
    {
        if (!_trackedTickers.Add(ticker))
            return;

        ticker.PropertyChanged += OnTrackedEditorChanged;
    }

    private void UnhookTicker(TickerItemEditorViewModel ticker)
    {
        if (!_trackedTickers.Remove(ticker))
            return;

        ticker.PropertyChanged -= OnTrackedEditorChanged;
    }

    private void ClearTrackedGroups()
    {
        foreach (TickerGroupEditorViewModel group in _trackedGroups.ToArray())
            UnhookGroup(group);

        _trackedTickers.Clear();
    }

    private void OnGroupTickersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (TickerItemEditorViewModel ticker in e.OldItems.OfType<TickerItemEditorViewModel>())
                UnhookTicker(ticker);
        }

        if (e.NewItems is not null)
        {
            foreach (TickerItemEditorViewModel ticker in e.NewItems.OfType<TickerItemEditorViewModel>())
                HookTicker(ticker);
        }

        if (_isLoadingState)
            return;

        InvalidateValidation();
    }

    private void OnTrackedEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingState || IsBusy)
            return;

        if (e.PropertyName is nameof(TickerItemEditorViewModel.ValidationState) or
            nameof(TickerItemEditorViewModel.ValidationMessage))
        {
            return;
        }

        InvalidateValidation();
    }

    private void InvalidateValidation()
    {
        if (_isLoadingState || IsBusy)
            return;

        _validatedSettings = null;
        _validatedQuoteSeeds = [];
        IsValidated = false;
        StatusMessage = "Configuration changed. Validate before saving.";
        ValidationSummary = "Run Validate to refresh structural checks and feed validation.";
        RefreshCommandStates();
    }

    private void ResetTickerValidationStates(SymbolValidationState state, string message)
    {
        foreach (TickerItemEditorViewModel ticker in Groups.SelectMany(group => group.Tickers))
        {
            if (string.IsNullOrWhiteSpace(ticker.Symbol))
            {
                ticker.ValidationState = SymbolValidationState.Pending;
                ticker.ValidationMessage = "Ticker slot is empty.";
                continue;
            }

            ticker.ValidationState = state;
            ticker.ValidationMessage = message;
        }
    }

    private async Task<YahooSymbolValidationResult> ValidateSymbolsAsync(
        AppSettings candidate,
        CancellationToken cancellationToken)
    {
        string[] symbols = candidate.Groups
            .SelectMany(static group => group.Tickers)
            .Where(static ticker => ticker.Enabled && !string.IsNullOrWhiteSpace(ticker.Symbol))
            .Select(static ticker => ticker.Symbol)
            .ToArray();
        if (symbols.Length == 0)
            return new YahooSymbolValidationResult([]);

        using YFinanceServerProcessManager manager = new();
        await using YFinanceProtocolRuntimeClient protocolClient = new();
        ManagedYFinanceRuntimeClient managedClient = new(manager, protocolClient, "DNPPV-2.0-Configuration");
        YahooSymbolValidationService validator = new(managedClient);
        IProgress<YahooSymbolValidationProgress> progress = new Progress<YahooSymbolValidationProgress>(UpdateTickerValidationProgress);
        return await validator.ValidateAsync(symbols, candidate.HttpTimeoutSeconds, progress, cancellationToken);
    }

    private void UpdateTickerValidationProgress(YahooSymbolValidationProgress progress)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateTickerValidationProgress(progress));
            return;
        }

        foreach (TickerItemEditorViewModel ticker in Groups.SelectMany(group => group.Tickers)
                     .Where(ticker => string.Equals(ticker.Symbol, progress.Symbol, StringComparison.OrdinalIgnoreCase)))
        {
            ticker.ValidationState = progress.IsValid ? SymbolValidationState.Valid : SymbolValidationState.Pending;
            ticker.ValidationMessage = progress.Message;
        }

        AppendValidationLog($"TICKER {progress.Symbol}: {progress.Message}");
    }

    private bool CancelValidation()
    {
        CancellationTokenSource? cancellation;
        lock (_validationCancellationGate)
            cancellation = _validationCancellation;
        if (cancellation is not { IsCancellationRequested: false })
            return false;

        StatusMessage = "Cancelling validation...";
        AppendValidationLog("CANCELLATION REQUESTED");
        cancellation.Cancel();
        return true;
    }

    private void MarkUncheckedTickersAsCancelled()
    {
        foreach (TickerItemEditorViewModel ticker in Groups.SelectMany(group => group.Tickers)
                     .Where(ticker => ticker.ValidationState == SymbolValidationState.Checking))
        {
            ticker.ValidationState = SymbolValidationState.Pending;
            ticker.ValidationMessage = "Validation cancelled before this ticker was checked.";
        }
    }

    private void AppendValidationLog(string entry)
    {
        string safeEntry = SensitiveDataRedactor.RedactSensitivePatterns(entry);
        ValidationLogText = string.IsNullOrWhiteSpace(ValidationLogText)
            ? safeEntry
            : $"{ValidationLogText}{Environment.NewLine}{safeEntry}";
    }

    private async Task RetryConnectivityAsync()
        => await RefreshConnectivityAsync(forceProbe: true);

    private async Task RefreshConnectivityAsync(bool forceProbe)
    {
        if (_disposed || Interlocked.CompareExchange(ref _connectivityRefreshRunning, 1, 0) != 0)
            return;

        RefreshRetryNetworkCommandState();
        try
        {
            if (forceProbe)
                _connectivityService.ForceProbe();

            bool available = await _connectivityService.IsInternetAvailableAsync();
            ApplyConnectivityResult(available);
        }
        catch (Exception exception)
        {
            TraceLog.WarnState(
                "Config.Connectivity",
                "ProbeUnavailable",
                [new("exception_type", exception.GetType().Name)]);
            ApplyConnectivityResult(false);
        }
        finally
        {
            Interlocked.Exchange(ref _connectivityRefreshRunning, 0);
            RefreshRetryNetworkCommandState();
        }
    }

    private void OnConnectivityChanged(object? sender, EventArgs eventArgs)
        => _ = RefreshConnectivityAsync(forceProbe: false);

    private void ApplyConnectivityResult(bool available)
    {
        if (_disposed)
            return;

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ApplyConnectivityResult(available));
            return;
        }

        IsNetworkAvailable = available;
        if (available)
        {
            if (!IsBusy)
                StatusMessage = "Internet connection available. Configure settings, then validate.";
            return;
        }

        if (!IsBusy)
        {
            StatusMessage = "Internet connection is required before validation can run.";
            ValidationSummary = "Configuration is temporarily locked. Restore connectivity and select Retry network.";
        }
    }

    private void RefreshRetryNetworkCommandState()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RetryNetworkCommand.NotifyCanExecuteChanged();
            return;
        }

        Dispatcher.UIThread.Post(RetryNetworkCommand.NotifyCanExecuteChanged);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _connectivityService.ConnectivityChanged -= OnConnectivityChanged;
        if (_ownsConnectivityService && _connectivityService is IDisposable disposable)
            disposable.Dispose();
        CancelValidation();
    }

    private void RequestClose()
    {
        if (Interlocked.Exchange(ref _closeRequested, 1) != 0)
            return;

        if (Dispatcher.UIThread.CheckAccess())
        {
            CloseRequested?.Invoke();
            return;
        }

        Dispatcher.UIThread.Post(() => CloseRequested?.Invoke());
    }

    private void MarkTickerValidationStatesFromCandidate(
        AppSettings candidate,
        YahooSymbolValidationResult? symbolValidation)
    {
        foreach (TickerItemEditorViewModel ticker in Groups.SelectMany(group => group.Tickers))
        {
            if (string.IsNullOrWhiteSpace(ticker.Symbol))
            {
                ticker.ValidationState = SymbolValidationState.Pending;
                ticker.ValidationMessage = "Ticker slot is empty.";
                continue;
            }

            if (symbolValidation is not null && symbolValidation.Entries.TryGetValue(ticker.Symbol, out YahooSymbolValidationEntry? entry))
            {
                ticker.ValidationState = entry.IsValid
                    ? SymbolValidationState.Valid
                    : entry.WasChecked ? SymbolValidationState.Invalid : SymbolValidationState.Pending;
                ticker.ValidationMessage = entry.IsValid
                    ? "Validated via YFinance.NET."
                    : entry.WasChecked ? entry.FailureReason : "Validation deferred; the runtime will retry.";
                continue;
            }

            ticker.ValidationState = SymbolValidationState.Pending;
            ticker.ValidationMessage = "Validation deferred; the runtime will retry.";
        }
    }

    private void ApplyProviderDisplayNames(YahooSymbolValidationResult? symbolValidation)
    {
        if (symbolValidation is null)
            return;

        foreach (TickerItemEditorViewModel ticker in Groups.SelectMany(group => group.Tickers))
        {
            if (string.IsNullOrWhiteSpace(ticker.Symbol) ||
                !symbolValidation.Entries.TryGetValue(ticker.Symbol, out YahooSymbolValidationEntry? entry) ||
                !entry.IsValid || string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                continue;
            }

            ticker.DisplayName = entry.DisplayName;
        }
    }

    private static QuoteSnapshot CloneQuote(QuoteSnapshot source)
        => new()
        {
            Symbol = source.Symbol,
            Last = source.Last,
            Change = source.Change,
            ChangePercent = source.ChangePercent,
            PreviousClose = source.PreviousClose,
            Currency = source.Currency,
            ExchangeTimeZoneId = source.ExchangeTimeZoneId,
            MarketSession = source.MarketSession,
            ProviderTimestampUtc = source.ProviderTimestampUtc,
            FetchTimestampUtc = source.FetchTimestampUtc,
            IsStale = source.IsStale
        };

    private void RefreshCommandStates()
    {
        ValidateCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        RevertCommand.NotifyCanExecuteChanged();
        AddGroupCommand.NotifyCanExecuteChanged();
        CancelValidationCommand.NotifyCanExecuteChanged();
        RefreshRetryNetworkCommandState();
    }
}
