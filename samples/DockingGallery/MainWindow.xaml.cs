using Digi21.WinUI.Docking;
using Digi21.WinUI.Docking.Serialization;
using Microsoft.UI.Xaml;

namespace DockingGallery;

public sealed partial class MainWindow : Window
{
    private readonly DockSiteLayoutSerializer serializer = new();
    private string? savedLayout;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSaveLayout(object sender, RoutedEventArgs e)
    {
        savedLayout = serializer.SaveToString(DockSite);
        LoadLayoutButton.IsEnabled = true;
        EventLog.Text = $"Layout saved ({savedLayout.Length} chars)";
    }

    private void OnLoadLayout(object sender, RoutedEventArgs e)
    {
        if (savedLayout is not null)
        {
            serializer.LoadFromString(DockSite, savedLayout);
            EventLog.Text = "Layout loaded";
        }
    }

    private void OnFloatOutput(object sender, RoutedEventArgs e)
    {
        if (!Output.IsOpen)
        {
            DockSite.DockToolWindow(Output, DockSide.Bottom);
        }

        Output.Float();
        EventLog.Text = $"Output state: {Output.State}";
    }

    private void OnDockFloating(object sender, RoutedEventArgs e)
    {
        foreach (var window in DockSite.ToolWindows.Where(w => w.State == DockingWindowState.Floating).ToList())
        {
            window.Dock();
        }

        EventLog.Text = "Floating windows docked";
    }

    private void OnWindowEvent(object? sender, DockingWindowEventArgs e)
    {
        EventLog.Text = $"{e.Window.Title}: last event at {DateTime.Now:HH:mm:ss}";
    }

    private void OnReopenSolutionExplorer(object sender, RoutedEventArgs e)
    {
        Reopen(SolutionExplorer, DockSide.Left);
    }

    private void OnReopenOutput(object sender, RoutedEventArgs e)
    {
        Reopen(Output, DockSide.Bottom);
    }

    private void Reopen(ToolWindow window, DockSide side)
    {
        if (window.IsOpen)
        {
            window.Activate();
        }
        else
        {
            DockSite.DockToolWindow(window, side);
        }
    }
}
