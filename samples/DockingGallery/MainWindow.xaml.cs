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

        // Attached from here, not from a Loaded handler: the trace has to be listening before the
        // dock site raises its own Loaded, which is where the ordering the panel exists to show
        // actually happens.
        Trace.Attach(this, DockSite, EventTrace);

        // Only needed by an application that closes its window from code, which this one does not;
        // it is here because it costs nothing and is the line that is missed until a File > Exit
        // command takes the process down with a floating window open.
        Closed += (_, _) => DockSite.CloseFloatingWindows();
    }

    private void OnNewDocument(object sender, RoutedEventArgs e)
    {
        var id = $"untitled{++newDocumentCount}";
        Documents.OpenDocument(CreateDocument(id, $"Untitled {newDocumentCount}"));
        EventLog.Text = $"Opened {id}";
    }

    // What a single click in a file list does in Visual Studio: the document opens in preview,
    // replacing whatever was being previewed instead of leaving a tab behind. Which documents open
    // this way is the application's decision, which is why it is a flag on the call.
    private void OnPreviewDocument(object sender, RoutedEventArgs e)
    {
        var id = $"untitled{++newDocumentCount}";
        Documents.OpenDocument(CreateDocument(id, $"Untitled {newDocumentCount}"), provisional: true);
        EventLog.Text = $"Previewing {id} — type in it and it is kept";
    }

    // The same thing the tab's own pin button does, from the outside: pinning is a property of the
    // document, so a toolbar, a command or a binding can drive it.
    private void OnPinDocument(object sender, RoutedEventArgs e)
    {
        if (DockSite.ActiveDocument is { } document)
        {
            document.IsPinned = !document.IsPinned;
            EventLog.Text = $"{document.Title} {(document.IsPinned ? "pinned" : "unpinned")}";
        }
    }

    // What a File menu's "Close All Tabs" does. Which documents survive is the library's rule, not
    // this application's: pinning a tab is what spares it.
    private void OnCloseDocuments(object sender, RoutedEventArgs e)
    {
        Documents.CloseDocuments(DocumentCloseScope.AllButPinned);
        EventLog.Text = $"{Documents.Documents.Count()} document(s) left";
    }

    private static DocumentWindow CreateDocument(string id, string title)
    {
        var editor = new TextBox
        {
            AcceptsReturn = true,
            BorderThickness = new Thickness(0),
            PlaceholderText = "Type here",
            TextWrapping = TextWrapping.Wrap,
        };

        var document = new DocumentWindow
        {
            Title = title,
            SerializationId = id,
            Content = editor,
        };

        // The promotion gesture the library cannot own: only the application knows what editing its
        // document means. The other four — double click, drag, pin, "keep open" — are the tab's own.
        editor.TextChanged += (_, _) => document.KeepOpen();

        return document;
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
