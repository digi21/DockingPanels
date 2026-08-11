using System.Collections.ObjectModel;
using Digi21.WinUI.Docking;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI.Text;

namespace DockingGallery;

/// <summary>
/// One line of the trace. Public because the panel binds to it with compiled bindings.
/// </summary>
public sealed class TraceEntry
{
    internal TraceEntry(string line, bool emphasized)
    {
        Line = line;
        Weight = emphasized ? FontWeights.SemiBold : FontWeights.Normal;
    }

    /// <summary>Gets the text of the line, already formatted with its elapsed time.</summary>
    public string Line { get; }

    /// <summary>Gets the weight the line is drawn with: markers and relocations stand out.</summary>
    public FontWeight Weight { get; }
}

/// <summary>
/// Live trace of the docking events in the order they actually happen, which is what an application
/// hosting content with a life cycle of its own — a swap chain, a video, a map — has to build
/// against. WinUI raises <c>Loaded</c> before <c>Unloaded</c> for a window that merely moves, so the
/// last event such content sees is the unload one; <c>Relocated</c> is the library's answer to that
/// and stands out in the trace.
/// </summary>
public sealed partial class EventTracePanel : UserControl
{
    private readonly List<Action> detach = [];
    private readonly DateTime start = DateTime.Now;

    /// <summary>Initializes a new instance of the <see cref="EventTracePanel"/> class.</summary>
    public EventTracePanel()
    {
        InitializeComponent();
    }

    /// <summary>Gets the lines of the trace, oldest first.</summary>
    public ObservableCollection<TraceEntry> Entries { get; } = [];

    // Starts recording.
    //
    // The panel is attached from the main window's constructor so that it is already listening when
    // the dock site raises its own Loaded, which is where the interesting ordering happens and
    // where an application restores its layout.
    internal void Attach(Window window, DockSite dockSite, ToolWindow self)
    {
        Subscribe(dockSite);

        // The panel holds a handler on every window it watches, and a handler is a strong reference
        // from the panel to the window. Left in place it would keep closed windows alive and turn a
        // real life-cycle leak into a trace that looks healthy, so everything is undone when the
        // panel's own window closes or the application shuts down.
        Listen<DockingWindowEventArgs>(
            h => dockSite.WindowClosed += h,
            h => dockSite.WindowClosed -= h,
            (_, e) =>
            {
                if (ReferenceEquals(e.Window, self))
                {
                    Detach();
                }
            });

        window.Closed += (_, _) => Detach();

        // A dock site's own Loaded fires before that of its descendants, which is why this line
        // comes first and why a window's back-pointer to its site is still null from here.
        dockSite.Loaded += (_, _) => Write("DockSite.Loaded", emphasized: true);
    }

    private void Subscribe(DockSite dockSite)
    {
        Listen<DockingWindowEventArgs>(
            h => dockSite.WindowOpened += h,
            h => dockSite.WindowOpened -= h,
            (_, e) => Write($"  [{Label(e.Window)}] Opened"));

        Listen<DockingWindowEventArgs>(
            h => dockSite.WindowClosed += h,
            h => dockSite.WindowClosed -= h,
            (_, e) => Write($"  [{Label(e.Window)}] Closed"));

        Listen<LayoutChangedEventArgs>(
            h => dockSite.LayoutChanged += h,
            h => dockSite.LayoutChanged -= h,
            (_, e) => Write($"  LayoutChanged: {e.Kind}"));

        // The windows declared in XAML register themselves as they load, which is after the dock
        // site's own Loaded, so the site sweeps its trees to answer this.
        foreach (var window in dockSite.ToolWindows.Cast<DockingWindow>().Concat(dockSite.Documents))
        {
            Watch(window, Label(window));
        }

        // The document area relocates as a whole when a load rebuilds the tree around it, which is
        // the notification an application hosting a swap chain there has to hang its render loop
        // off. It is not a DockingWindow, so it carries its own Relocated.
        if (dockSite.DocumentHost is { } documentHost)
        {
            EventHandler relocated = (_, _) => Write("  [DocumentHost] RELOCATED", emphasized: true);
            documentHost.Relocated += relocated;
            detach.Add(() => documentHost.Relocated -= relocated);
        }
    }

    private void Watch(DockingWindow window, string name)
    {
        RoutedEventHandler loaded = (_, _) => Write($"  [{name}] Loaded");
        RoutedEventHandler unloaded = (_, _) => Write($"  [{name}] Unloaded");
        EventHandler relocated = (_, _) => Write($"  [{name}] RELOCATED", emphasized: true);

        window.Loaded += loaded;
        window.Unloaded += unloaded;
        window.Relocated += relocated;

        detach.Add(() =>
        {
            window.Loaded -= loaded;
            window.Unloaded -= unloaded;
            window.Relocated -= relocated;
        });
    }

    // Subscribes to an event of the dock site and remembers how to undo it.
    private void Listen<T>(
        Action<EventHandler<T>> add,
        Action<EventHandler<T>> remove,
        EventHandler<T> handler)
    {
        add(handler);
        detach.Add(() => remove(handler));
    }

    private void Detach()
    {
        foreach (var undo in detach)
        {
            undo();
        }

        detach.Clear();
    }

    private static string Label(DockingWindow window)
    {
        return window.SerializationId ?? window.Title;
    }

    private void Write(string message, bool emphasized = false)
    {
        var line = $"{(DateTime.Now - start).TotalMilliseconds,7:F0}ms  {message}";
        Entries.Add(new TraceEntry(line, emphasized || message.Contains("RELOCATED", StringComparison.Ordinal)));

        // Scrolling has to wait for the repeater to lay the new line out.
        DispatcherQueue.TryEnqueue(() =>
        {
            TraceScroller.UpdateLayout();
            TraceScroller.ChangeView(null, TraceScroller.ScrollableHeight, null, disableAnimation: true);
        });
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        Entries.Clear();
    }
}
