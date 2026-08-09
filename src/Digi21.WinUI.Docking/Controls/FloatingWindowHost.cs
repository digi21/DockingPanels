using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Digi21.WinUI.Docking;

/// <summary>
/// A top-level window hosting tool windows floated out of a <see cref="DockSite"/>. The windows
/// keep living in a <see cref="ToolWindowContainer"/>, so tabs and the title bar chrome behave
/// exactly as they do when docked; only the container's parent changes, and the tool window
/// elements (with their content and state) travel unchanged.
/// </summary>
/// <remarks>
/// The window deliberately has no system title bar: the container's own title bar acts as the
/// caption, which is what allows dragging the floating window back into the dock site with the
/// same dock guides as an ordinary re-dock. The resize border is kept, so the window can still
/// be resized and moved across monitors like any other window.
/// </remarks>
internal sealed partial class FloatingWindowHost
{
    private readonly DockSite site;
    private readonly Window window;
    private readonly Grid root;
    private long selectionToken = -1;
    private bool closingSelf;

    internal FloatingWindowHost(DockSite site, ToolWindowContainer container, RectInt32 bounds)
    {
        this.site = site;
        Container = container;

        root = new Grid { RequestedTheme = site.ActualTheme };
        root.Children.Add(container);

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
        container.ItemsChanged += OnContainerItemsChanged;

        // A second window does not inherit the theme of the one the dock site lives in.
        site.ActualThemeChanged += OnSiteThemeChanged;
        selectionToken = container.RegisterPropertyChangedCallback(
            ToolWindowContainer.SelectedItemProperty,
            (_, _) => UpdateTitle());

        UpdateTitle();
        window.Activate();
    }

    /// <summary>
    /// Picks the size a container should get when it floats: the size it has while docked,
    /// kept within bounds that stay usable as a window (a wide, shallow bottom pane would
    /// otherwise float as an unwieldy letterbox).
    /// </summary>
    internal static (double Width, double Height) PreferredSize(ToolWindowContainer? container)
    {
        return (Clamp(container?.ActualWidth, 380, 280, 640), Clamp(container?.ActualHeight, 300, 220, 480));

        static double Clamp(double? value, double fallback, double min, double max)
        {
            return value is { } size && double.IsFinite(size) && size > 0
                ? Math.Clamp(size, min, max)
                : fallback;
        }
    }

    /// <summary>
    /// Keeps window bounds inside a monitor that actually exists: a layout saved on a
    /// multi-monitor setup can name coordinates that are off-screen when it is restored on a
    /// different one.
    /// </summary>
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

    /// <summary>Gets the container holding the floating tool windows.</summary>
    internal ToolWindowContainer Container { get; }

    /// <summary>Gets the window handle, used to tell what a drop landed on.</summary>
    internal IntPtr Handle { get; }

    /// <summary>Gets or sets where the group re-docks when it is pinned back into the layout.</summary>
    internal DockRestoreHint? RestoreHint { get; set; }

    /// <summary>Gets the window bounds in screen pixels.</summary>
    internal RectInt32 Bounds
    {
        get
        {
            var position = window.AppWindow.Position;
            var size = window.AppWindow.Size;
            return new RectInt32(position.X, position.Y, size.Width, size.Height);
        }
    }

    /// <summary>Moves the window to the given screen position, keeping its size.</summary>
    internal void MoveTo(PointInt32 position) => window.AppWindow.Move(position);

    /// <summary>Brings the floating window to the front.</summary>
    internal void Activate() => window.Activate();

    /// <summary>
    /// Releases the container and closes the window without touching the hosted tool windows,
    /// so they can be docked back into the site or moved to another floating window.
    /// </summary>
    internal void ReleaseAndClose()
    {
        Detach();
        root.Children.Remove(Container);
        CloseWindow();
    }

    /// <summary>
    /// Closes the floating window as if the user had closed it: every hosted tool window is
    /// closed through the normal cancelable path. Returns whether the window actually closed.
    /// </summary>
    internal bool CloseHostedWindows()
    {
        foreach (var hosted in Container.Items.ToList())
        {
            hosted.Close();
        }

        if (Container.Items.Count > 0)
        {
            return false;
        }

        // Closing the last window empties the container, which closes this host.
        return true;
    }

    private void OnContainerItemsChanged(object? sender, EventArgs e)
    {
        UpdateTitle();

        if (Container.Items.Count == 0)
        {
            ReleaseAndClose();
            return;
        }

        // Windows dropped into this container are docked as far as the container knows;
        // being hosted here is what makes them floating.
        foreach (var hosted in Container.Items)
        {
            hosted.State = DockingWindowState.Floating;
        }
    }

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
            site.SetActiveWindow(Container.SelectedItem);
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

    private void CloseWindow()
    {
        closingSelf = true;
        site.RemoveFloatingHost(this);
        window.Close();
    }

    private void Detach()
    {
        if (selectionToken >= 0)
        {
            Container.UnregisterPropertyChangedCallback(ToolWindowContainer.SelectedItemProperty, selectionToken);
            selectionToken = -1;
        }

        Container.ItemsChanged -= OnContainerItemsChanged;
        window.Activated -= OnActivated;
        window.AppWindow.Closing -= OnAppWindowClosing;
        site.ActualThemeChanged -= OnSiteThemeChanged;
    }

    private void UpdateTitle()
    {
        window.Title = Container.SelectedItem?.Title ?? Container.Items.FirstOrDefault()?.Title ?? string.Empty;
    }
}
