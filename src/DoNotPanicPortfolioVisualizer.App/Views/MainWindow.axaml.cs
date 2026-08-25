using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;

namespace DoNotPanicPortfolioVisualizer.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Screen? screen = Screens.ScreenFromWindow(this);
        if (screen is null)
            return;

        // Screen work areas are physical pixels.  Keep the configuration dialog on the
        // owner's monitor and leave its desktop chrome unobscured at every DPI scale.
        double scale = Math.Max(1d, RenderScaling);
        double availableWidth = Math.Max(1d, (screen.WorkingArea.Width - 24d) / scale);
        double availableHeight = Math.Max(1d, (screen.WorkingArea.Height - 24d) / scale);
        MaxWidth = availableWidth;
        MaxHeight = availableHeight;
        MinWidth = Math.Min(MinWidth, availableWidth);
        MinHeight = Math.Min(MinHeight, availableHeight);
        Width = Math.Min(Width, availableWidth);
        Height = Math.Min(Height, availableHeight);
    }
}
