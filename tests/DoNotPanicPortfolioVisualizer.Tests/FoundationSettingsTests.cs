using DoNotPanicPortfolioVisualizer.Core.Constants;
using DoNotPanicPortfolioVisualizer.Core.Enums;
using DoNotPanicPortfolioVisualizer.Core.Models;
using DoNotPanicPortfolioVisualizer.Core.Services;
using DoNotPanicPortfolioVisualizer.Core.Validation;
using DoNotPanicPortfolioVisualizer.Shared.Diagnostics;
using DoNotPanicPortfolioVisualizer.Shared.Helpers;

namespace DoNotPanicPortfolioVisualizer.Tests;

[Collection("EnvironmentSerial")]
public sealed class FoundationSettingsTests
{
    [Fact]
    public void CreateSettings_UsesCurrentRssDefaultAndManagedPaths()
    {
        using EnvironmentScope scope = new();
        string productRoot = CreateTempPath("defaults-root");
        scope.Set("DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT", productRoot);

        AppSettings settings = Defaults.CreateSettings();

        Assert.Equal("https://www.france24.com/en/business/rss", settings.NewsFeedUrl);
        Assert.Equal(NewsScrollerMode.RssFeed, settings.NewsScrollerMode);
        Assert.StartsWith(productRoot, settings.HistoricalCacheRootFolder, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(productRoot, settings.BackgroundImageFolder, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(PathHelper.AppLocalDataFolderName, productRoot, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SettingsValidator_DefaultSettings_ReturnsNoErrors()
    {
        using EnvironmentScope scope = new();
        scope.Set("DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT", CreateTempPath("validator-defaults"));
        AppSettings settings = Defaults.CreateSettings();

        IReadOnlyList<string> errors = new SettingsValidator().Validate(settings);

        Assert.Empty(errors);
    }

    [Fact]
    public void SettingsValidator_RssModeRequiresValidUrl()
    {
        AppSettings settings = Defaults.CreateSettings();
        settings.NewsScrollerMode = NewsScrollerMode.RssFeed;
        settings.NewsFeedUrl = "not a url";

        IReadOnlyList<string> errors = new SettingsValidator().Validate(settings);

        Assert.Contains(errors, error => error.Contains("News feed URL", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SettingsValidator_SummarizedModeDoesNotRequireValidRssUrl()
    {
        AppSettings settings = Defaults.CreateSettings();
        settings.NewsScrollerMode = NewsScrollerMode.SummarizedFinancialNews;
        settings.NewsFeedUrl = "not a url";

        IReadOnlyList<string> errors = new SettingsValidator().Validate(settings);

        Assert.DoesNotContain(errors, error => error.Contains("News feed URL", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AppSettingsNormalizer_NormalizesLegacyHistoryCacheAndInvalidFeed()
    {
        using EnvironmentScope scope = new();
        scope.Set("DONOTPANICPORTFOLIOVISUALIZER2_LOCALDATA_ROOT", CreateTempPath("normalizer-root"));

        AppSettings settings = new()
        {
            HistoricalCacheRootFolder = Defaults.GetLegacyHistoricalCacheFolder(),
            NewsFeedUrl = "not a url",
            Groups = []
        };

        AppSettings normalized = AppSettingsNormalizer.Normalize(settings);

        Assert.Equal(Defaults.GetHistoricalCacheFolder(), normalized.HistoricalCacheRootFolder);
        Assert.Equal("https://www.france24.com/en/business/rss", normalized.NewsFeedUrl);
        Assert.NotEmpty(normalized.Groups);
    }

    [Fact]
    public void CircularTraceSettings_ParsesCurrentEnvironmentVariableAndCaches()
    {
        using EnvironmentScope scope = new();
        scope.Set(CircularTraceSettings.MaxTraceMegabytesEnvironmentVariable, "8");
        int cachedBytes = 0;

        int first = CircularTraceSettings.ResolveCachedMaxTraceBytes(ref cachedBytes);
        scope.Set(CircularTraceSettings.MaxTraceMegabytesEnvironmentVariable, "16");
        int second = CircularTraceSettings.ResolveCachedMaxTraceBytes(ref cachedBytes);

        Assert.Equal(8 * 1024 * 1024, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void SensitiveDataRedactor_RedactsAssignmentsBearerTokensAndProviderKeys()
    {
        string secretValue = "sk-" + new string('a', 24);
        string message = $"apiKey=supersecret Bearer abcdefghijklmnopqrstuvwxyz {secretValue}";

        string redacted = SensitiveDataRedactor.RedactSensitivePatterns(message);

        Assert.Contains("apiKey=<redacted>", redacted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bearer <redacted>", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain(secretValue, redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("supersecret", redacted, StringComparison.Ordinal);
    }

    private static string CreateTempPath(string suffix)
    {
        string path = Path.Combine(Path.GetTempPath(), "DNPPV2Tests", Guid.NewGuid().ToString("N"), suffix, AppDataRootResolver.AppLocalDataFolderName);
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previousValues = new(StringComparer.Ordinal);

        public void Set(string name, string? value)
        {
            if (!_previousValues.ContainsKey(name))
                _previousValues[name] = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);

            Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
        }

        public void Dispose()
        {
            foreach ((string name, string? value) in _previousValues)
                Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process);
        }
    }
}
