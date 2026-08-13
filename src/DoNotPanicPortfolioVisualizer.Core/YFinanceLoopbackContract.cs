namespace DoNotPanicPortfolioVisualizer.Core;

public static class YFinanceLoopbackContract
{
    public const string LoopbackHost = "127.0.0.1";
    public const int DefaultPort = 14871;

    public static Uri BaseUri { get; } = new($"http://{LoopbackHost}:{DefaultPort}/", UriKind.Absolute);
}
