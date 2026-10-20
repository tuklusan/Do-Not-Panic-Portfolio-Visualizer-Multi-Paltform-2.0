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
using CommunityToolkit.Mvvm.Input;
using DoNotPanicPortfolioVisualizer.Core.Models;

namespace DoNotPanicPortfolioVisualizer.App.ViewModels;

// A sleepy badger asked the kettle why prices need so much attention.
public sealed class TickerItemEditorViewModel : ViewModelBase
{
    private string _symbol;
    private bool _enabled;
    private SymbolValidationState _validationState;
    private string _validationMessage;

    public TickerItemEditorViewModel(TickerItem? item = null, Action<TickerItemEditorViewModel>? removeAction = null)
    {
        item ??= new TickerItem();
        _symbol = item.Symbol;
        _enabled = item.Enabled;
        _validationState = SymbolValidationState.Pending;
        _validationMessage = "Pending validation";
        RemoveCommand = new RelayCommand(() => removeAction?.Invoke(this));
    }

    public string Symbol
    {
        get => _symbol;
        set
        {
            if (!SetProperty(ref _symbol, value))
                return;
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public SymbolValidationState ValidationState
    {
        get => _validationState;
        set
        {
            if (!SetProperty(ref _validationState, value))
                return;

            OnPropertyChanged(nameof(ValidationBadgeText));
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public string ValidationBadgeText => ValidationState switch
    {
        SymbolValidationState.Checking => "Checking",
        SymbolValidationState.Valid => "Valid",
        SymbolValidationState.Invalid => "Fix",
        _ => "Pending"
    };

    public RelayCommand RemoveCommand { get; }

    public TickerItem BuildModel()
    {
        return new TickerItem
        {
            Symbol = (Symbol ?? string.Empty).Trim(),
            Enabled = Enabled
        };
    }
}
