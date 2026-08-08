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

        if (Environment.GetCommandLineArgs().Contains("--auto-test"))
        {
            RunAutoTest();
        }
    }

    // Temporary scripted validation of auto-hide, removed after milestone 6.
    private void RunAutoTest()
    {
        var step = 0;
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(2);
        timer.Tick += (_, _) =>
        {
            step++;
            try
            {
                switch (step)
                {
                    case 1:
                        SolutionExplorer.AutoHide();
                        break;
                    case 2:
                        ClassView.Activate();
                        break;
                    case 3:
                        var log = new System.Text.StringBuilder();
                        DumpTree(DockSite, 0, log);
                        File.AppendAllText(Path.Combine(Path.GetTempPath(), "DockingGallery-crash.log"), log.ToString());
                        timer.Stop();
                        break;
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    Path.Combine(Path.GetTempPath(), "DockingGallery-crash.log"),
                    $"[step {step}] {ex}\n\n");
                timer.Stop();
            }
        };
        timer.Start();
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

    private static void DumpTree(Microsoft.UI.Xaml.DependencyObject node, int depth, System.Text.StringBuilder log)
    {
        if (depth > 7)
        {
            return;
        }

        if (node is Microsoft.UI.Xaml.FrameworkElement fe)
        {
            var text = node is Microsoft.UI.Xaml.Controls.TextBlock tb ? $" '{tb.Text}'" : string.Empty;
            log.AppendLine($"{new string(' ', depth * 2)}{node.GetType().Name}{text} off=({fe.ActualOffset.X:F0},{fe.ActualOffset.Y:F0}) size=({fe.ActualWidth:F0}x{fe.ActualHeight:F0}) vis={fe.Visibility}");
        }

        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            DumpTree(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i), depth + 1, log);
        }
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
