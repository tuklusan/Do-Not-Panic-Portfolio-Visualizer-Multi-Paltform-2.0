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
using CommunityToolkit.Mvvm.Input;
using DoNotPanicPortfolioVisualizer.Core.Constants;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;

namespace DoNotPanicPortfolioVisualizer.App.ViewModels;

public sealed class TickerGroupEditorViewModel : ViewModelBase
{
    private readonly Action<TickerGroupEditorViewModel>? _removeAction;
    private string _id;
    private string _name;
    private bool _enabled;
    private double _speedValue;
    private RenderMode _renderMode;
    private ScrollDirection _direction;
    private double _rowHeight;

    public TickerGroupEditorViewModel(TickerGroup? group = null, Action<TickerGroupEditorViewModel>? removeAction = null)
    {
        group ??= Defaults.CreateEmptyTickerGroup(0);
        _removeAction = removeAction;
        _id = string.IsNullOrWhiteSpace(group.Id) ? Guid.NewGuid().ToString("N") : group.Id;
        _name = group.Name;
        _enabled = group.Enabled;
        _speedValue = Math.Clamp(
            group.Speed <= 0 ? Defaults.DefaultTapeBaseSpeed : group.Speed,
            Defaults.MinTapeSpeed,
            Defaults.MaxTapeSpeed);
        _renderMode = group.RenderMode;
        _direction = group.Direction;
        _rowHeight = group.RowHeight <= 0 ? 56.0 : group.RowHeight;
        Tickers = new ObservableCollection<TickerItemEditorViewModel>(
            (group.Tickers ?? [])
                .Take(Defaults.MaxTickersPerTape)
                .Select(item => new TickerItemEditorViewModel(item, RemoveTicker)));

        DirectionOptions = [ScrollDirection.Left, ScrollDirection.Right];
        AddTickerCommand = new RelayCommand(AddTicker);
        RemoveGroupCommand = new RelayCommand(() => _removeAction?.Invoke(this));
    }

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public double SpeedValue
    {
        get => _speedValue;
        set
        {
            double normalized = Math.Round(Math.Clamp(value, Defaults.MinTapeSpeed, Defaults.MaxTapeSpeed), 3);
            if (!SetProperty(ref _speedValue, normalized))
                return;

            OnPropertyChanged(nameof(SpeedLabel));
        }
    }

    public string SpeedLabel => $"{SpeedValue:0.00}x";

    public RenderMode RenderMode
    {
        get => _renderMode;
        set => SetProperty(ref _renderMode, value);
    }

    public ScrollDirection Direction
    {
        get => _direction;
        set => SetProperty(ref _direction, value);
    }

    public double RowHeight
    {
        get => _rowHeight;
        set => SetProperty(ref _rowHeight, value);
    }

    public string TickerSlotsLabel => $"{Tickers.Count}/{Defaults.MaxTickersPerTape} tickers";

    public ObservableCollection<TickerItemEditorViewModel> Tickers { get; }

    public IReadOnlyList<ScrollDirection> DirectionOptions { get; }

    public RelayCommand AddTickerCommand { get; }

    public RelayCommand RemoveGroupCommand { get; }

    private void AddTicker()
    {
        if (Tickers.Count >= Defaults.MaxTickersPerTape)
            return;

        Tickers.Add(new TickerItemEditorViewModel(removeAction: RemoveTicker));
        OnPropertyChanged(nameof(TickerSlotsLabel));
    }

    private void RemoveTicker(TickerItemEditorViewModel item)
    {
        Tickers.Remove(item);
        OnPropertyChanged(nameof(TickerSlotsLabel));
    }

    public TickerGroup ToModel(int displayIndex, List<string> errors)
    {
        string normalizedName = string.IsNullOrWhiteSpace(Name)
            ? Defaults.GetDefaultTapeName(displayIndex + 1)
            : Name.Trim();
        List<TickerItem> tickers = [];
        foreach (TickerItemEditorViewModel editor in Tickers.Take(Defaults.MaxTickersPerTape))
        {
            if (string.IsNullOrWhiteSpace(editor.Symbol))
                continue;

            editor.TryBuildModel(normalizedName, errors, out TickerItem ticker);
            tickers.Add(ticker);
        }

        return new TickerGroup
        {
            Id = string.IsNullOrWhiteSpace(Id) ? Guid.NewGuid().ToString("N") : Id,
            Name = normalizedName,
            Enabled = Enabled,
            Speed = SpeedValue,
            RenderMode = RenderMode,
            Direction = Direction,
            RowHeight = RowHeight,
            Tickers = tickers
        };
    }
}
