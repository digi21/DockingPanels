using Digi21.WinUI.Docking;
using Digi21.WinUI.Docking.Serialization;
using Microsoft.UI.Xaml;

namespace DockingGallery;

/// <summary>
/// What a scenario needs to drive the gallery: the windows by the ids they are saved under, a way
/// to step one layout pass at a time, and a way to end an unattended run.
/// </summary>
internal sealed class ScenarioContext
{
    private readonly Window host;

    internal ScenarioContext(Window host, DockSite site, Action<string> log)
    {
        this.host = host;
        Site = site;
        Log = log;
    }

    internal DockSite Site { get; }

    internal Action<string> Log { get; }

    internal DockSiteLayoutSerializer Serializer { get; } = new();

    // Where an unattended run reads and writes the layout it is testing.
    internal static string LayoutFile =>
        Environment.GetEnvironmentVariable("DOCKPROBE_LAYOUT") ?? @"C:\Temp\gallery-layout.xml";

    internal ToolWindow Tool(string id) =>
        Site.ToolWindows.First(w => w.SerializationId == id);

    // Runs the next step one layout pass later. What a step does before and after this call is the
    // whole subject of most of these scenarios.
    internal void Next(Action step)
    {
        host.DispatcherQueue.TryEnqueue(() => step());
    }

    // Resizes the host window, which is what makes the auto-hide flyout recompute its placement.
    internal void Resize(int width, int height)
    {
        host.AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
    }

    internal void CloseAfter(int delayMs, Action before)
    {
        var timer = host.DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(delayMs);
        timer.IsRepeating = false;
        timer.Tick += (_, _) =>
        {
            before();
            host.Close();
        };
        timer.Start();
    }
}

/// <summary>
/// A named reproduction, run either from a button in the trace panel or unattended from DOCKPROBE.
/// </summary>
/// <param name="Name">Short name, and the value DOCKPROBE takes to run it at startup.</param>
/// <param name="Description">What it does and what to look for, shown as the button's tooltip.</param>
/// <param name="AtLoaded">Whether its point is the timing of the dock site's Loaded.</param>
/// <param name="AutoCloseMs">How long an unattended run waits before closing the window.</param>
/// <param name="Run">The reproduction itself.</param>
internal sealed record Scenario(
    string Name,
    string Description,
    bool AtLoaded,
    int AutoCloseMs,
    Action<ScenarioContext> Run);

/// <summary>
/// The reproductions kept from the investigations, one per bug that was only visible in a running
/// application. Each is worth keeping because the next change to the order of events will break one
/// of them first.
/// </summary>
internal static class Scenarios
{
    internal static IReadOnlyList<Scenario> All { get; } =
    [
        new(
            "SAVE",
            "Builds a layout that differs from the XAML one — one group auto-hidden, one window "
                + "floating — and writes it to disk for the load scenarios to read.",
            AtLoaded: false,
            AutoCloseMs: 1500,
            Save),

        new(
            "L1",
            "Restores that layout straight from the dock site's Loaded, which is where an "
                + "application has a dock site to restore into. Everything must come back; a bare "
                + "DocumentHost and an empty registry is the bug this reproduces.",
            AtLoaded: true,
            AutoCloseMs: 3000,
            LoadNow),

        new(
            "L2",
            "The same load one layout pass later. This used to be the only way it worked and is "
                + "kept as the control: it must be indistinguishable from L1.",
            AtLoaded: true,
            AutoCloseMs: 3000,
            LoadDeferred),

        new(
            "NOOP",
            "Saves the current layout and loads it straight back. Nothing changed, so nothing may "
                + "move: no Unloaded and no RELOCATED between the two markers.",
            AtLoaded: false,
            AutoCloseMs: 3000,
            ReloadIdentical),

        new(
            "FLOAT",
            "Pulls Output into a floating window. The control case that always relocated "
                + "correctly, for comparison against the loads.",
            AtLoaded: false,
            AutoCloseMs: 3000,
            Float),

        new(
            "AUTOHIDE",
            "Collapses two panes to their edges back to back, with no dispatcher hop between them. "
                + "Spreading them one per layout pass was a consumer's workaround; both must land "
                + "from a single pass.",
            AtLoaded: true,
            AutoCloseMs: 3000,
            AutoHideBoth),

        new(
            "EDGE",
            "Auto-hides a window, opens its flyout and resizes the host window. The flyout must "
                + "stay on the edge it slides from instead of jumping.",
            AtLoaded: false,
            AutoCloseMs: 4000,
            FlyoutEdgeOnResize),
    ];

    internal static Scenario? Find(string? name)
    {
        return string.IsNullOrWhiteSpace(name)
            ? null
            : All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static void Save(ScenarioContext context)
    {
        context.Next(() =>
        {
            context.Tool("solutionExplorer").AutoHide();
            context.Tool("output").Float();

            context.Next(() =>
            {
                context.Serializer.SaveToFile(context.Site, ScenarioContext.LayoutFile);
                context.Log($"written to {ScenarioContext.LayoutFile}");
            });
        });
    }

    private static void LoadNow(ScenarioContext context)
    {
        context.Log("LoadFromFile, no deferral");
        context.Serializer.LoadFromFile(context.Site, ScenarioContext.LayoutFile);
        context.Log("load returned");
    }

    private static void LoadDeferred(ScenarioContext context)
    {
        context.Next(() =>
        {
            context.Log("LoadFromFile, one pass later");
            context.Serializer.LoadFromFile(context.Site, ScenarioContext.LayoutFile);
            context.Log("load returned");
        });
    }

    private static void ReloadIdentical(ScenarioContext context)
    {
        var xml = context.Serializer.SaveToString(context.Site);
        context.Log("--- reload of the identical layout starts ---");
        context.Serializer.LoadFromString(context.Site, xml);
        context.Log("--- reload done ---");
    }

    private static void Float(ScenarioContext context)
    {
        context.Next(() => context.Tool("output").Float());
    }

    private static void AutoHideBoth(ScenarioContext context)
    {
        // Two windows of the same pane would not do: AutoHide() collapses the pane a window sits
        // in, taking its neighbours along, so the second call would find nothing left to do. These
        // two live in different panes and collapse to different edges.
        context.Tool("solutionExplorer").AutoHide();
        context.Tool("output").AutoHide();
        context.Log("both AutoHide() calls returned from the same pass");
    }

    private static void FlyoutEdgeOnResize(ScenarioContext context)
    {
        var window = context.Tool("solutionExplorer");
        window.AutoHide();

        context.Next(() =>
        {
            window.Activate();
            context.Log("flyout open on the left edge; resizing the host window");

            context.Next(() =>
            {
                context.Resize(1100, 700);
                context.Log("resized — the flyout must still hang off the left edge");
            });
        });
    }
}
