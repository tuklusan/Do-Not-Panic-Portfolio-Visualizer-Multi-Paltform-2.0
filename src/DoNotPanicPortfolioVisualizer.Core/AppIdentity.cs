namespace DoNotPanicPortfolioVisualizer.Core;

public static class AppIdentity
{
    public const string ProductName = "DO NOT PANIC PORTFOLIO VISUALIZER 2.0";
    public const string ProductDisplayName = "DO NOT PANIC PORTFOLIO VISUALIZER";
    public const string PublisherName = "SANYALnet Labs";
    public const string AuthorName = "Supratim Sanyal";
    public const string LicenseName = "SANYALnet Labs Non-Commercial License";

    public const string LocalDataFolderName = "DoNotPanicPortfolioVisualizer2";
    public const string LegacyProductLocalDataFolderName = "DoNotPanicPortfolioVisualizer";
    public const string LegacyPortfolioSaverLocalDataFolderName = "PortfolioSaver";

    public const string LocalDataRootOverrideEnvironmentVariable = "DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT";
    public const string DeprecatedLocalDataRootOverrideEnvironmentVariable = "DONOTPANICPORTFOLIOVISUALIZER_LOCALDATA_ROOT";
    public const string DeprecatedPortfolioSaverLocalDataRootOverrideEnvironmentVariable = "PORTFOLIOSAVER_LOCALDATA_ROOT";
    public const string DeprecatedPortfolioSaverAppDataRootOverrideEnvironmentVariable = "PORTFOLIOSAVER_APPDATA_ROOT";

    public static IReadOnlyList<string> DeprecatedOverrideEnvironmentVariables { get; } =
    [
        DeprecatedLocalDataRootOverrideEnvironmentVariable,
        DeprecatedPortfolioSaverLocalDataRootOverrideEnvironmentVariable,
        DeprecatedPortfolioSaverAppDataRootOverrideEnvironmentVariable
    ];
}
