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
        Reopen(SolutionExplorer, LeftContainer);
    }

    private void OnReopenOutput(object sender, RoutedEventArgs e)
    {
        Reopen(Output, BottomContainer);
    }

    private void Reopen(ToolWindow window, ToolWindowContainer preferredContainer)
    {
        if (window.IsOpen)
        {
            window.Activate();
            return;
        }

        // Until programmatic docking arrives (DockToolWindow), reopen into the preferred
        // container if it is still part of the layout, else into any container that is.
        var target = preferredContainer.XamlRoot is not null
            ? preferredContainer
            : DockSite.ToolWindows.FirstOrDefault(w => w.IsOpen)?.Container;

        if (target is not null)
        {
            target.Items.Add(window);
            window.Activate();
        }
    }
}
