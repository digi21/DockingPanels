using Digi21.WinUI.Docking;
using Digi21.WinUI.Docking.Serialization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

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

    // Arranging the open documents from code, in one step: the documents are dealt into the groups in the order they
    // are in now, and what the layout does not name (more documents than groups) becomes tabs of the last group.
    private void OnArrangeTabs(object sender, RoutedEventArgs e) => Arrange(DocumentLayout.Group(Documents.Documents), "all in tabs");

    // Every document the dock site knows, closed ones included: a closed document the layout names is opened where it says.
    private void OnArrangeKnown(object sender, RoutedEventArgs e) => Arrange(DocumentLayout.Group(DockSite.Documents), "every known document in tabs");

    // The same moves one document at a time, which dragging a tab onto a guide does by hand: the active document goes to the
    // side of (or into the group of) the first other open document.
    private void OnDockActiveRight(object sender, RoutedEventArgs e) => DockActive(DockSide.Right, "to the right of");

    private void OnDockActiveBelow(object sender, RoutedEventArgs e) => DockActive(DockSide.Bottom, "below");

    private void OnAttachActive(object sender, RoutedEventArgs e) => DockActive(side: null, "into the group of");

    private void DockActive(DockSide? side, string description)
    {
        if (DockSite.ActiveDocument is not { } active)
        {
            EventLog.Text = "Activate a document first";
            return;
        }

        var target = Documents.Documents.FirstOrDefault(d => !ReferenceEquals(d, active));
        if (target is null)
        {
            EventLog.Text = "Open a second document first";
            return;
        }

        if (side is { } dockSide)
        {
            DockSite.DockDocument(active, target, dockSide);
        }
        else
        {
            DockSite.AttachDocument(active, target);
        }

        EventLog.Text = $"Docked {active.Title} {description} {target.Title}";
    }

    private void OnArrangeColumns(object sender, RoutedEventArgs e)
    {
        var documents = Documents.Documents.ToArray();
        Arrange(
            DocumentLayout.Split(
                Orientation.Horizontal,
                DocumentLayout.Group(documents.Take(1)),
                DocumentLayout.Group(documents.Skip(1).Take(1)),
                DocumentLayout.Group(documents.Skip(2))),
            "three columns");
    }

    private void OnArrangeGrid(object sender, RoutedEventArgs e)
    {
        var documents = Documents.Documents.ToArray();
        Arrange(
            DocumentLayout.Split(
                Orientation.Vertical,
                DocumentLayout.Split(
                    Orientation.Horizontal,
                    DocumentLayout.Group(documents.Take(1)),
                    DocumentLayout.Group(documents.Skip(1).Take(1))),
                DocumentLayout.Split(
                    Orientation.Horizontal,
                    DocumentLayout.Group(documents.Skip(2).Take(1)),
                    DocumentLayout.Group(documents.Skip(3)))),
            "a grid");
    }

    private void OnArrangeMain(object sender, RoutedEventArgs e)
    {
        var documents = Documents.Documents.ToArray();
        Arrange(
            DocumentLayout.Split(
                Orientation.Horizontal,
                DocumentLayout.Group(documents.Take(1)).WithRelativeSize(0.6),
                DocumentLayout.Split(
                    Orientation.Vertical,
                    DocumentLayout.Group(documents.Skip(1).Take(1)),
                    DocumentLayout.Group(documents.Skip(2))).WithRelativeSize(0.4)),
            "one large, two small");
    }

    private void Arrange(DocumentLayout layout, string description)
    {
        Documents.Arrange(layout);
        EventLog.Text = $"Arranged the documents: {description}";
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

    // Unpinning one panel out of a shared tab group, which the pin button of a title bar does not
    // do: that one takes the whole group to the edge, on purpose. An application that hides a panel
    // when the mode it belongs to ends has to leave the panels beside it alone.
    private void OnUnpinClassView(object sender, RoutedEventArgs e)
    {
        if (ClassView.State == DockingWindowState.AutoHide)
        {
            ClassView.Dock();
            EventLog.Text = $"Class View pinned back — Solution Explorer stayed {SolutionExplorer.State}";
            return;
        }

        ClassView.AutoHide(AutoHideScope.Window);
        EventLog.Text = $"Class View {ClassView.State}, Solution Explorer still {SolutionExplorer.State}";
    }

    // What a user upgrading sees: a layout saved before a panel existed, loaded while the panel is
    // open. The file cannot say where a window it never heard of goes, so the window says it —
    // Output is declared PreferredDockSide="Bottom" and lands there, not at the left edge.
    private void OnRescueOutput(object sender, RoutedEventArgs e)
    {
        Output.Close();
        var layoutWithoutOutput = serializer.SaveToString(DockSite);
        DockSite.DockToolWindow(Output, DockSide.Bottom);

        var behavior = serializer.UnresolvedWindowBehavior;
        serializer.UnresolvedWindowBehavior = UnresolvedWindowBehavior.DockLeft;
        serializer.LoadFromString(DockSite, layoutWithoutOutput);
        serializer.UnresolvedWindowBehavior = behavior;

        EventLog.Text = $"Output was not in the layout: rescued to {Output.PreferredDockSide}";
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

    // Visual Studio's own edges do not open on the way past: the panel waits to be asked for. Which
    // it is depends on the application — an edge the pointer crosses often is one that should stay
    // still — so it is a setting and not a rule.
    private void OnToggleAutoHideTrigger(object sender, RoutedEventArgs e)
    {
        DockSite.AutoHideOpenTrigger = ((ToggleButton)sender).IsChecked == true
            ? AutoHideOpenTrigger.Click
            : AutoHideOpenTrigger.Pointer;

        EventLog.Text = $"Auto-hide tabs open on: {DockSite.AutoHideOpenTrigger}";
    }

    // The middle ground between the two triggers: the pointer still opens a panel, but only once it
    // rests on the tab.
    private void OnToggleAutoHideOpenDelay(object sender, RoutedEventArgs e)
    {
        DockSite.AutoHideOpenDelay = ((ToggleButton)sender).IsChecked == true
            ? TimeSpan.FromMilliseconds(400)
            : TimeSpan.Zero;

        EventLog.Text = $"Hover previews wait: {DockSite.AutoHideOpenDelay.TotalMilliseconds} ms";
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

    // What a log does when it is written to while nobody is looking: it lights its tab instead of
    // opening itself over whatever the user is doing. Already on screen, the line is the news.
    private void OnWriteToOutput(object sender, RoutedEventArgs e)
    {
        OutputText.Text += $"\nWritten at {DateTime.Now:HH:mm:ss}";

        EventLog.Text = Output.RequestAttention()
            ? "Output is asking for attention"
            : $"Output did not ask for attention (open: {Output.IsOpen}, on screen: {Output.IsContentVisible})";
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
