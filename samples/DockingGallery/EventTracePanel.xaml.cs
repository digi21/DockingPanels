using System.Collections.ObjectModel;
using System.Text;
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
/// Live trace of the docking events, with the scenarios that reproduce the ordering bugs found so
/// far. Four of the five bugs fixed in this library were only visible in a running application, and
/// every one of them was a question about the order of Loaded, Unloaded and Relocated.
/// </summary>
public sealed partial class EventTracePanel : UserControl
{
    private readonly List<Action> detach = [];
    private readonly StringBuilder logFile = new();
    private readonly DateTime start = DateTime.Now;

    private DockSite site = null!;
    private Window host = null!;
    private string? logPath;

    /// <summary>Initializes a new instance of the <see cref="EventTracePanel"/> class.</summary>
    public EventTracePanel()
    {
        InitializeComponent();
    }

    /// <summary>Gets the lines of the trace, oldest first.</summary>
    public ObservableCollection<TraceEntry> Entries { get; } = [];

    // Starts recording, and runs the scenario named by DOCKPROBE if there is one.
    //
    // The panel is attached from the main window's constructor so that it is already listening when
    // the dock site raises its own Loaded, which is where the interesting ordering happens and
    // where an application restores its layout.
    internal void Attach(Window window, DockSite dockSite, ToolWindow self)
    {
        host = window;
        site = dockSite;
        // An empty variable is an unset one. A shell that clears it by assigning "" would otherwise
        // leave an empty path here, and writing to it throws from inside a layout event, which
        // takes the whole application down.
        var configured = Environment.GetEnvironmentVariable("DOCKPROBE_LOG");
        logPath = string.IsNullOrWhiteSpace(configured) ? null : configured;

        Subscribe(dockSite);

        // The harness holds a handler on every window it watches, and a handler is a strong
        // reference from the panel to the window. Left in place it would keep closed windows alive
        // and turn a real life-cycle leak into a trace that looks healthy, so everything is undone
        // when the panel's own window closes or the application shuts down.
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

        var scenario = Environment.GetEnvironmentVariable("DOCKPROBE");

        dockSite.Loaded += (_, _) =>
        {
            Mark($"DockSite.Loaded — {Describe()}");

            if (Scenarios.Find(scenario) is { } headless)
            {
                Run(headless, headless.AutoCloseMs);
            }
        };

        ScenarioButtons.ItemsSource = Scenarios.All.Select(Build).ToList();
        Status.Text = logPath is null
            ? "Set DOCKPROBE=<scenario> and DOCKPROBE_LOG=<file> to run one at startup, unattended."
            : $"Writing to {logPath}";
    }

    private Button Build(Scenario scenario)
    {
        var button = new Button
        {
            Content = scenario.Name,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        ToolTipService.SetToolTip(button, scenario.Description);
        button.Click += (_, _) => Run(scenario, autoCloseMs: 0);
        return button;
    }

    private void Run(Scenario scenario, int autoCloseMs)
    {
        Mark($"=== {scenario.Name}: {scenario.Description} ===");

        // A scenario that reproduces a startup-ordering bug only means what it says when it runs
        // from the dock site's Loaded. Clicking its button exercises the same calls, one or more
        // layout passes later, which is precisely the difference the bugs lived in.
        if (scenario.AtLoaded && autoCloseMs == 0)
        {
            Mark($"    (running after load; for the real timing, DOCKPROBE={scenario.Name})");
        }

        var context = new ScenarioContext(host, site, message => Write($"  {message}"));
        scenario.Run(context);

        if (autoCloseMs > 0)
        {
            context.CloseAfter(autoCloseMs, () => Mark($"closing — {Describe()}"));
        }
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

        // Reading the registry is itself the fix for the last bug: a window declared in XAML
        // registers itself when it loads, which is after the dock site's own Loaded, so the site
        // has to sweep its trees to answer this from here.
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

    // A one-line summary of where everything is, which is what tells a passing run from a failing
    // one when the trace alone is ambiguous.
    private string Describe()
    {
        var states = site.ToolWindows.Select(w => $"{w.SerializationId}:{(w.IsOpen ? w.State.ToString() : "closed")}");
        return $"child={site.Child?.GetType().Name ?? "<null>"}, "
            + $"tools={site.ToolWindows.Count}, documents={site.Documents.Count} "
            + $"[{string.Join(", ", states)}]";
    }

    private void Mark(string message)
    {
        Write(message, emphasized: true);
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

        if (logPath is null)
        {
            return;
        }

        logFile.AppendLine(line);
        File.WriteAllText(logPath, logFile.ToString());
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        Entries.Clear();
    }
}
