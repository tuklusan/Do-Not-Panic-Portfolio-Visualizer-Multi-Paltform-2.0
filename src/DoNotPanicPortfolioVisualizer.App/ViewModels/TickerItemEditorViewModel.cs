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

public sealed class TickerItemEditorViewModel : ViewModelBase
{
    private string _symbol;
    private string _displayName;
    private string _quantityText;
    private string _costBasisText;
    private string _currency;
    private bool _enabled;
    private SymbolValidationState _validationState;
    private string _validationMessage;

    public TickerItemEditorViewModel(TickerItem? item = null, Action<TickerItemEditorViewModel>? removeAction = null)
    {
        item ??= new TickerItem();
        _symbol = item.Symbol;
        _displayName = item.DisplayName;
        _quantityText = item.Quantity?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        _costBasisText = item.CostBasis?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        _currency = string.IsNullOrWhiteSpace(item.Currency) ? "USD" : item.Currency.Trim();
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
            string previousSymbol = _symbol;
            if (!SetProperty(ref _symbol, value))
                return;

            if (string.IsNullOrWhiteSpace(DisplayName))
                return;

            if (!string.Equals(previousSymbol.Trim(), _symbol.Trim(), StringComparison.OrdinalIgnoreCase))
                DisplayName = string.Empty;
        }
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string QuantityText
    {
        get => _quantityText;
        set => SetProperty(ref _quantityText, value);
    }

    public string CostBasisText
    {
        get => _costBasisText;
        set => SetProperty(ref _costBasisText, value);
    }

    public string Currency
    {
        get => _currency;
        set => SetProperty(ref _currency, value);
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

    public bool TryBuildModel(string tapeName, List<string> errors, out TickerItem ticker)
    {
        ticker = new TickerItem
        {
            Symbol = (Symbol ?? string.Empty).Trim(),
            DisplayName = (DisplayName ?? string.Empty).Trim(),
            Currency = string.IsNullOrWhiteSpace(Currency) ? "USD" : Currency.Trim().ToUpperInvariant(),
            Enabled = Enabled
        };

        if (!string.IsNullOrWhiteSpace(QuantityText))
        {
            if (decimal.TryParse(
                    QuantityText,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal quantity))
            {
                ticker.Quantity = quantity;
            }
            else
            {
                errors.Add($"{tapeName}: quantity for '{ticker.Symbol}' must be a valid number.");
            }
        }

        if (!string.IsNullOrWhiteSpace(CostBasisText))
        {
            if (decimal.TryParse(
                    CostBasisText,
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal costBasis))
            {
                ticker.CostBasis = costBasis;
            }
            else
            {
                errors.Add($"{tapeName}: cost basis for '{ticker.Symbol}' must be a valid number.");
            }
        }

        return errors.Count == 0;
    }
}
