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
using DoNotPanicPortfolioVisualizer.Core.Constants;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Core.Services;
using DoNotPanicPortfolioVisualizer.Core.Validation;
using DoNotPanicPortfolioVisualizer.Data.Services;
using DoNotPanicPortfolioVisualizer.Shared;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;

namespace DoNotPanicPortfolioVisualizer.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly SettingsFileService _settingsFileService;
    private readonly SettingsValidator _settingsValidator;
    private readonly NewsFeedValidationService _newsFeedValidationService;
    private readonly AiNewsAccessValidationService _aiNewsAccessValidationService;
    private readonly SemaphoreSlim _validationGate = new(1, 1);
    private AppSettings _loadedSettings;
    private AppSettings? _validatedSettings;
    private bool _isBusy;
    private bool _isValidated;
    private bool _isLoadingState;
    private string _statusMessage;
    private string _validationSummary;
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
    {
        _settingsFileService = new SettingsFileService();
        _settingsValidator = new SettingsValidator();
        _newsFeedValidationService = new NewsFeedValidationService();
        _aiNewsAccessValidationService = new AiNewsAccessValidationService();
        _loadedSettings = _settingsFileService.Load();
        _statusMessage = $"{PortfolioVersion.DisplayName} configuration ready";
        _validationSummary = "Validate before saving changes.";
        _customBackgroundImageFolder = string.Empty;
        _newsFeedUrl = Defaults.DefaultNewsFeedUrl;
        _aiApiKey = string.Empty;
        _aiEndpointUrl = Defaults.DefaultAiEndpointUrl;
        _aiModelId = Defaults.DefaultAiModelId;
        Groups = [];
        ValidateCommand = new AsyncRelayCommand(ValidateAsync, () => CanValidate);
        SaveCommand = new RelayCommand(Save, () => CanSave);
        RevertCommand = new RelayCommand(Revert, () => CanRevert);
        AddGroupCommand = new RelayCommand(AddGroup, () => CanAddGroup);
        ApplyLoadedSettings(_loadedSettings);
    }

    public string ProductTitle => PortfolioVersion.DisplayName;
    public string WindowSubtitle => "Avalonia Configuration Window";
    public string RuntimeLine => ".NET 10 + Avalonia 12.1.1";

    public ObservableCollection<TickerGroupEditorViewModel> Groups { get; }

    public IAsyncRelayCommand ValidateCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand RevertCommand { get; }
    public IRelayCommand AddGroupCommand { get; }

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

    public bool CanValidate => !IsBusy;
    public bool CanSave => !IsBusy && IsValidated && _validatedSettings is not null;
    public bool CanRevert => !IsBusy;
    public bool CanAddGroup => !IsBusy && Groups.Count < Defaults.MaxTapeCount;

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

        try
        {
            gateHeld = await _validationGate.WaitAsync(0).ConfigureAwait(false);
            if (!gateHeld)
                return;

            IsBusy = true;
            ResetTickerValidationStates(SymbolValidationState.Checking, "Validation in progress");

            List<string> errors = [];
            AppSettings candidate = BuildCandidateSettings(errors);
            errors.AddRange(_settingsValidator.Validate(candidate));

            string feedNote = string.Empty;
            using ConfigConnectivityService connectivity = new();
            bool networkAvailable = errors.Count == 0 &&
                await connectivity.IsInternetAvailableAsync().ConfigureAwait(false);
            if (errors.Count == 0 && candidate.NewsScrollerMode == NewsScrollerMode.RssFeed)
            {
                NewsFeedValidationResult feedValidation = await _newsFeedValidationService.ValidateAsync(
                    candidate.NewsFeedUrl,
                    candidate.HttpTimeoutSeconds,
                    networkAvailable).ConfigureAwait(false);

                candidate.NewsFeedUrl = feedValidation.ResolvedFeedUrl;
                if (!string.Equals(NewsFeedUrl, candidate.NewsFeedUrl, StringComparison.Ordinal))
                    NewsFeedUrl = candidate.NewsFeedUrl;

                feedNote = string.IsNullOrWhiteSpace(feedValidation.Message)
                    ? "RSS feed check passed."
                    : feedValidation.Message;
            }
            else if (errors.Count == 0 && candidate.NewsScrollerMode == NewsScrollerMode.SummarizedFinancialNews)
            {
                AiNewsAccessValidationResult aiValidation = await _aiNewsAccessValidationService.ValidateAsync(
                    candidate,
                    networkAvailable).ConfigureAwait(false);
                string aiMessage = SensitiveDataRedactor.RedactSensitivePatterns(aiValidation.Message);
                if (!aiValidation.IsValid)
                    errors.Add(aiMessage);

                feedNote = string.IsNullOrWhiteSpace(aiMessage)
                    ? "AI access check passed."
                    : aiMessage;
            }

            if (errors.Count > 0)
            {
                ResetTickerValidationStates(SymbolValidationState.Invalid, "Fix validation issues");
                _validatedSettings = null;
                IsValidated = false;
                StatusMessage = errors[0];
                ValidationSummary = string.Join(Environment.NewLine, errors);
                return;
            }

            MarkTickerValidationStatesFromCandidate(candidate);
            _validatedSettings = candidate.Clone();
            IsValidated = true;
            StatusMessage = "Validation passed. Review the settings and click Save to persist them.";
            ValidationSummary = feedNote;
        }
        catch (Exception exception)
        {
            TraceLog.WarnState(
                "Config.Validation",
                "ValidationServiceUnavailable",
                [new("exception_type", exception.GetType().Name)]);
            ResetTickerValidationStates(SymbolValidationState.Invalid, "Validation unavailable");
            _validatedSettings = null;
            IsValidated = false;
            StatusMessage = "Validation could not complete.";
            ValidationSummary = "A configuration validation service was unavailable. Review the settings and try again.";
        }
        finally
        {
            if (gateHeld)
            {
                IsBusy = false;
                _validationGate.Release();
            }
        }
    }

    private void Save()
    {
        if (_validatedSettings is null)
            return;

        _settingsFileService.Save(_validatedSettings);
        _loadedSettings = _validatedSettings.Clone();
        ApplyLoadedSettings(_loadedSettings);
        _validatedSettings = _loadedSettings.Clone();
        IsValidated = true;
        StatusMessage = $"{PortfolioVersion.Version} settings saved at {DateTime.Now:T}.";
        ValidationSummary = "Settings were persisted through the portable storage layer.";
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

    private void MarkTickerValidationStatesFromCandidate(AppSettings candidate)
    {
        foreach (TickerItemEditorViewModel ticker in Groups.SelectMany(group => group.Tickers))
        {
            if (string.IsNullOrWhiteSpace(ticker.Symbol))
            {
                ticker.ValidationState = SymbolValidationState.Pending;
                ticker.ValidationMessage = "Ticker slot is empty.";
                continue;
            }

            ticker.ValidationState = SymbolValidationState.Valid;
            ticker.ValidationMessage = candidate.NewsScrollerMode == NewsScrollerMode.RssFeed
                ? "Structure and RSS checks passed."
                : "Structure checks passed. Live AI/symbol validation is queued for a later CR.";
        }
    }

    private void RefreshCommandStates()
    {
        ValidateCommand.NotifyCanExecuteChanged();
        SaveCommand.NotifyCanExecuteChanged();
        RevertCommand.NotifyCanExecuteChanged();
        AddGroupCommand.NotifyCanExecuteChanged();
    }
}
