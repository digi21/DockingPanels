using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Digi21.WinUI.Docking;

// A top-level window hosting tool windows floated out of a DockSite. It holds a layout tree of its
// own, so windows can be docked, split and tabbed inside it exactly as they are inside the dock
// site; the elements (with their content and state) travel unchanged between both.
//
// The window deliberately has no system title bar: with a single pane the pane's own title bar acts
// as the caption, which is what allows dragging the floating window back into the dock site with
// the same dock guides as an ordinary re-dock. Once it holds several panes, that role moves to a
// FloatingWindowCaption shown above them. The resize border is kept, so the window can still be
// resized and moved across monitors like any other window.
internal sealed partial class FloatingWindowHost
{
    private readonly Window window;
    private readonly FloatingWindowRoot root;
    private readonly Dictionary<DockingWindowContainer, long> trackedContainers = [];
    private bool closingSelf;

    internal FloatingWindowHost(DockSite site, UIElement content, RectInt32 bounds)
    {
        Site = site;
        root = new FloatingWindowRoot(site, this) { RequestedTheme = site.ActualTheme };

        window = new Window { Content = root };
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(window);

        if (ScreenInterop.GetWindowHandle(site) is { } owner)
        {
            ScreenInterop.SetOwner(Handle, owner);
        }

        window.AppWindow.MoveAndResize(bounds);

        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = true;
        }

        window.AppWindow.Closing += OnAppWindowClosing;
        window.Activated += OnActivated;

        // A second window does not inherit the theme of the one the dock site lives in.
        site.ActualThemeChanged += OnSiteThemeChanged;

        root.Child = content;
        SyncWithLayout();
        window.Activate();
    }

    // Picks the size a container should get when it floats: the size it has while docked, kept
    // within bounds that stay usable as a window (a wide, shallow bottom pane would otherwise float
    // as an unwieldy letterbox).
    internal static (double Width, double Height) PreferredSize(FrameworkElement? element)
    {
        return (Clamp(element?.ActualWidth, 380, 280, 640), Clamp(element?.ActualHeight, 300, 220, 480));

        static double Clamp(double? value, double fallback, double min, double max)
        {
            return value is { } size && double.IsFinite(size) && size > 0
                ? Math.Clamp(size, min, max)
                : fallback;
        }
    }

    // Keeps window bounds inside a monitor that actually exists: a layout saved on a multi-monitor
    // setup can name coordinates that are off-screen when it is restored on a different one.
    internal static RectInt32 ClampToDisplay(RectInt32 bounds)
    {
        var area = DisplayArea.GetFromRect(bounds, DisplayAreaFallback.Nearest);
        if (area is null)
        {
            return bounds;
        }

        var work = area.WorkArea;
        var width = Math.Max(160, Math.Min(bounds.Width, work.Width));
        var height = Math.Max(120, Math.Min(bounds.Height, work.Height));

        return new RectInt32(
            Math.Clamp(bounds.X, work.X, Math.Max(work.X, work.X + work.Width - width)),
            Math.Clamp(bounds.Y, work.Y, Math.Max(work.Y, work.Y + work.Height - height)),
            width,
            height);
    }

    // Gets the dock site the floating window belongs to.
    internal DockSite Site { get; }

    // Gets the docking surface of this window, the drop target of drags over it.
    internal IDockSurface Surface => root;

    // Gets or sets the root of the layout tree hosted by this window.
    internal UIElement? LayoutChild
    {
        get => root.Child;
        set => ((IDockSurface)root).LayoutChild = value;
    }

    // Gets the element that sizes a re-dock of this window, and the drag ghost's title.
    internal FrameworkElement? LayoutElement => root.Child as FrameworkElement;

    // Gets the container of a window hosting a single pane, or null when windows have been docked
    // inside it and it holds a tree of panes.
    internal DockingWindowContainer? SinglePane => root.Child as DockingWindowContainer;

    // Gets the windows hosted by this window, across all its panes.
    internal IEnumerable<DockingWindow> Windows => LayoutTree.Windows(root.Child);

    // Gets the window handle, used to tell what a drop landed on.
    internal IntPtr Handle { get; }

    // Gets or sets where the group re-docks when it is pinned back into the layout.
    internal DockRestoreHint? RestoreHint { get; set; }

    // Gets the window bounds in screen pixels.
    internal RectInt32 Bounds
    {
        get
        {
            var position = window.AppWindow.Position;
            var size = window.AppWindow.Size;
            return new RectInt32(position.X, position.Y, size.Width, size.Height);
        }
    }

    // Moves the window to the given screen position, keeping its size.
    internal void MoveTo(PointInt32 position) => window.AppWindow.Move(position);

    // Brings the floating window to the front.
    internal void Activate() => window.Activate();

    // Docks every window of this floating window back where it was floated from.
    internal void DockHome() => LayoutManager.DockFloatingHost(Site, this, DockTarget.Home);

    // Releases the layout tree and closes the window without touching the hosted tool windows, so
    // they can be docked back into the site or moved to another floating window.
    //
    // successor: the element whose window takes the foreground when this one had it, which is the
    // surface the tree is moving to. Defaults to the dock site's window.
    internal void ReleaseAndClose(FrameworkElement? successor = null)
    {
        Detach();
        root.Child = null;
        CloseWindow(successor);
    }

    // Closes the floating window as if the user had closed it: every hosted tool window is closed
    // through the normal cancelable path. Returns whether the window actually closed.
    internal bool CloseHostedWindows()
    {
        foreach (var hosted in Windows.ToList())
        {
            hosted.Close();
        }

        if (Windows.Any())
        {
            return false;
        }

        // Closing the last window empties the layout tree, which closes this host.
        return true;
    }

    // Brings the host back in step with its layout tree after a mutation: keeps track of the
    // containers inside it, marks their windows as floating, updates the caption and closes the
    // window once nothing is left in it.
    internal void SyncWithLayout()
    {
        if (closingSelf)
        {
            return;
        }

        var containers = LayoutTree.Containers(root.Child).ToList();
        TrackContainers(containers);

        var windows = LayoutTree.Windows(root.Child).ToList();
        if (windows.Count == 0)
        {
            ReleaseAndClose();
            return;
        }

        // Windows dropped into a container are docked as far as the container knows;
        // being hosted here is what makes them floating.
        foreach (var hosted in windows)
        {
            hosted.State = DockingWindowState.Floating;
            hosted.DockSite = Site;
        }

        var title = PrimaryWindow(containers)?.Title ?? string.Empty;
        window.Title = title;

        // With a single tool pane its title bar is the window's caption; several panes, or a
        // pane without a title bar of its own such as a document group, need a caption, since
        // otherwise nothing stands for the whole window and it could not even be moved.
        var singleCaptionedPane = containers.Count == 1 && SinglePane?.ProvidesWindowCaption == true;
        root.SetCaption(title, !singleCaptionedPane);
    }

    private void TrackContainers(List<DockingWindowContainer> containers)
    {
        foreach (var tracked in trackedContainers.Keys.Where(c => !containers.Contains(c)).ToList())
        {
            tracked.ItemsChanged -= OnContainerItemsChanged;
            tracked.UnregisterPropertyChangedCallback(DockingWindowContainer.SelectedItemProperty, trackedContainers[tracked]);
            trackedContainers.Remove(tracked);
        }

        foreach (var container in containers.Where(c => !trackedContainers.ContainsKey(c)))
        {
            container.ItemsChanged += OnContainerItemsChanged;
            trackedContainers[container] = container.RegisterPropertyChangedCallback(
                DockingWindowContainer.SelectedItemProperty,
                (_, _) => SyncWithLayout());
        }
    }

    // Picks the window whose title names the whole floating window.
    private DockingWindow? PrimaryWindow(List<DockingWindowContainer> containers)
    {
        if (Site.ActiveWindow is { } active && containers.Any(c => c.Items.Contains(active)))
        {
            return active;
        }

        var first = containers.FirstOrDefault();
        return first?.SelectedItem ?? first?.Items.FirstOrDefault();
    }

    private void OnContainerItemsChanged(object? sender, EventArgs e) => SyncWithLayout();

    private void OnSiteThemeChanged(FrameworkElement sender, object args)
    {
        root.RequestedTheme = sender.ActualTheme;
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState != WindowActivationState.Deactivated)
        {
            // Straight to the dock site: going through DockingWindow.Activate would activate
            // this window again and bounce between the two.
            Site.SetActiveWindow(PrimaryWindow(LayoutTree.Containers(root.Child).ToList()));
        }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs e)
    {
        if (closingSelf)
        {
            return;
        }

        // The user closed the window (there is no caption button, but Alt+F4 and the system
        // menu still work): close the hosted windows, honoring cancelable close handlers.
        e.Cancel = !CloseHostedWindows();
    }

    private void CloseWindow(FrameworkElement? successor = null)
    {
        closingSelf = true;
        Site.RemoveFloatingHost(this);

        // This window is about to be destroyed: pass the foreground on before it goes, or the
        // application ends up behind the one that was underneath it.
        ScreenInterop.HandOverForeground(Handle, successor ?? Site);

        window.Close();
    }

    private void Detach()
    {
        TrackContainers([]);
        window.Activated -= OnActivated;
        window.AppWindow.Closing -= OnAppWindowClosing;
        Site.ActualThemeChanged -= OnSiteThemeChanged;
    }
}
