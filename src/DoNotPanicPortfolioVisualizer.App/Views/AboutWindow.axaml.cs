// Copyright (c) 2026 Supratim Sanyal of SANYALnet Labs.
// Based on original work by Supratim Sanyal of SANYALnet Labs.
// Governed by the SANYALnet Labs Non-Commercial License in the root LICENSE file.

using Avalonia.Controls;
using Avalonia.Interactivity;
using DoNotPanicPortfolioVisualizer.Core;
using DoNotPanicPortfolioVisualizer.Shared;
using DoNotPanicPortfolioVisualizer.Shared.Licensing;

namespace DoNotPanicPortfolioVisualizer.App.Views;

public partial class AboutWindow : Window
{
    public string VersionText => $"Version: {PortfolioVersion.Version}";
    public string PublisherText => $"Publisher: {AppIdentity.PublisherName}";
    public string AuthorText => $"Author: {AppIdentity.AuthorName}";
    public string LicenseText => $"License: {AppIdentity.LicenseName}";
    public string FullLicenseText => ProjectLicenseService.GetLicenseText();

    public AboutWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
