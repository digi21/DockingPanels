using Digi21.WinUI.Docking;
using Digi21.WinUI.Docking.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DockingGallery;

public sealed partial class MainWindow : Window
{
    private readonly DockSiteLayoutSerializer serializer = new();
    private string? savedLayout;
    private int newDocumentCount;

    public MainWindow()
    {
        InitializeComponent();

        // Documents created at runtime are gone after a restart unless the application can
        // recreate them: the serializer asks for the ones it cannot match by id.
        serializer.DocumentResolving += (_, e) =>
        {
            if (e.Id.StartsWith("untitled", StringComparison.Ordinal))
            {
                e.Document = CreateDocument(e.Id, $"{e.Id}.txt");
            }
        };
    }

    private void OnNewDocument(object sender, RoutedEventArgs e)
    {
        var id = $"untitled{++newDocumentCount}";
        Documents.OpenDocument(CreateDocument(id, $"Untitled {newDocumentCount}"));
        EventLog.Text = $"Opened {id}";
    }

    private static DocumentWindow CreateDocument(string id, string title)
    {
        return new DocumentWindow
        {
            Title = title,
            SerializationId = id,
            Content = new TextBox
            {
                AcceptsReturn = true,
                BorderThickness = new Thickness(0),
                PlaceholderText = "Type here",
                TextWrapping = TextWrapping.Wrap,
            },
        };
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

    /// <summary>
    /// Switches the whole window between light and dark. The docking chrome follows because its
    /// brushes live in theme dictionaries, and floating windows follow the dock site's theme.
    /// </summary>
    private void OnToggleTheme(object sender, RoutedEventArgs e)
    {
        RootGrid.RequestedTheme = RootGrid.ActualTheme == ElementTheme.Dark
            ? ElementTheme.Light
            : ElementTheme.Dark;

        EventLog.Text = $"Theme: {RootGrid.RequestedTheme}";
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
