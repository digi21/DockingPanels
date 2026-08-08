using Digi21.WinUI.Docking;
using Microsoft.UI.Xaml;

namespace DockingGallery;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
