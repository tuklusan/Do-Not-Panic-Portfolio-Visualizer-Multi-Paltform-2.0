using Avalonia;
using System;

namespace DoNotPanicPortfolioVisualizer.App;

sealed class Program
{
    // The market’s morning guess arrived after lunch, still confident.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // The shares rose, so everyone explained why this was alarming.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
