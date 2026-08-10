using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Base class for windows that participate in a docking layout, such as <see cref="ToolWindow"/>.
/// The window's <see cref="ContentControl.Content"/> travels with it when it is re-docked.
/// </summary>
public abstract partial class DockingWindow : ContentControl, IRelocatable
{
    /// <summary>
    /// Raised after a docking operation has moved this window to a different place in the XAML
    /// tree — docked elsewhere, tabbed into another pane, floated, auto-hidden or restored by a
    /// layout load — once the tree has settled. Content with a life cycle of its own (a
    /// <c>SwapChainPanel</c>, a <c>WebView2</c>, a render loop) should hang off this event instead
    /// of off <see cref="FrameworkElement.Loaded"/> and <see cref="FrameworkElement.Unloaded"/>,
    /// which WinUI raises in that order for an element that never left the tree.
    /// </summary>
    public event EventHandler? Relocated;

    /// <summary>Identifies the <see cref="Title"/> dependency property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(DockingWindow), new PropertyMetadata(string.Empty));

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(IconSource), typeof(DockingWindow), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="SerializationId"/> dependency property.</summary>
    public static readonly DependencyProperty SerializationIdProperty = DependencyProperty.Register(
        nameof(SerializationId), typeof(string), typeof(DockingWindow), new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="CanClose"/> dependency property.</summary>
    public static readonly DependencyProperty CanCloseProperty = DependencyProperty.Register(
        nameof(CanClose), typeof(bool), typeof(DockingWindow), new PropertyMetadata(true));

    /// <summary>Identifies the <see cref="CanDragWindow"/> dependency property.</summary>
    public static readonly DependencyProperty CanDragWindowProperty = DependencyProperty.Register(
        nameof(CanDragWindow), typeof(bool), typeof(DockingWindow), new PropertyMetadata(true));

    /// <summary>Identifies the <see cref="CanFloat"/> dependency property.</summary>
    public static readonly DependencyProperty CanFloatProperty = DependencyProperty.Register(
        nameof(CanFloat), typeof(bool), typeof(DockingWindow), new PropertyMetadata(true));

    /// <summary>Identifies the <see cref="CanAutoHide"/> dependency property.</summary>
    public static readonly DependencyProperty CanAutoHideProperty = DependencyProperty.Register(
        nameof(CanAutoHide), typeof(bool), typeof(DockingWindow), new PropertyMetadata(true));

    /// <summary>Identifies the <see cref="IsOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(DockingWindow), new PropertyMetadata(false));

    /// <summary>Identifies the <see cref="IsActive"/> dependency property.</summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(DockingWindow), new PropertyMetadata(false));

    /// <summary>Identifies the <see cref="IsSelected"/> dependency property.</summary>
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(DockingWindow), new PropertyMetadata(false));

    /// <summary>Identifies the <see cref="State"/> dependency property.</summary>
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(DockingWindowState), typeof(DockingWindow), new PropertyMetadata(DockingWindowState.Docked));

    private bool pendingOpenedNotification;
    private DockSite? dockSite;
    private Action? pendingOperations;

    /// <summary>Initializes a new instance of the <see cref="DockingWindow"/> class.</summary>
    protected DockingWindow()
    {
        Loaded += OnLoaded;
    }

    /// <summary>Gets or sets the text shown in the window's title bar and tab.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the icon shown in the window's tab.</summary>
    public IconSource? Icon
    {
        get => (IconSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets the stable identifier used by the layout serializer to match this window
    /// across sessions. Required for layout persistence.
    /// </summary>
    public string? SerializationId
    {
        get => (string?)GetValue(SerializationIdProperty);
        set => SetValue(SerializationIdProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the window shows a close button and can be closed.</summary>
    public bool CanClose
    {
        get => (bool)GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the window can be dragged to a new docking position.</summary>
    public bool CanDragWindow
    {
        get => (bool)GetValue(CanDragWindowProperty);
        set => SetValue(CanDragWindowProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the window can be floated into a top-level window.</summary>
    public bool CanFloat
    {
        get => (bool)GetValue(CanFloatProperty);
        set => SetValue(CanFloatProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the window shows a pin button and can auto-hide.</summary>
    public bool CanAutoHide
    {
        get => (bool)GetValue(CanAutoHideProperty);
        set => SetValue(CanAutoHideProperty, value);
    }

    /// <summary>Gets a value indicating whether the window is currently part of the layout.</summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        internal set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Gets a value indicating whether the window is the active (focused) window of its dock site.</summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        internal set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Gets a value indicating whether the window is the selected tab of its container.</summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        internal set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Gets the current docking state of the window.</summary>
    public DockingWindowState State
    {
        get => (DockingWindowState)GetValue(StateProperty);
        internal set => SetValue(StateProperty, value);
    }

    /// <summary>Gets the container that currently hosts this window, or <see langword="null"/> when closed.</summary>
    public DockingWindowContainer? Container { get; internal set; }

    // Gets the dock site this window belongs to, or null while it is not part of one.
    //
    // Resolved from the tree on demand rather than only in FrameworkElement.Loaded: a dock site's
    // own Loaded is raised before that of the windows it contains, so an application setting up its
    // layout from there would otherwise find no site here and see its calls do nothing.
    internal DockSite? DockSite
    {
        get => dockSite ??= this.FindSurface()?.Site;
        set => dockSite = value;
    }

    // Gets or sets a value indicating whether the window is being moved to a new docking position.
    // While set, attach/detach notifications do not raise opened/closed events.
    internal bool IsRelocating { get; set; }

    /// <summary>
    /// Selects the window in its container and makes it the active window of its dock site.
    /// If the window is auto-hidden, its flyout is opened. Does nothing while the window is
    /// closed.
    /// </summary>
    public void Activate()
    {
        if (!IsOpen)
        {
            return;
        }

        if (this is ToolWindow tool && State == DockingWindowState.AutoHide)
        {
            DockSite?.ShowAutoHideFlyout(tool);
            return;
        }

        Container?.Select(this);

        if (State == DockingWindowState.Floating)
        {
            DockSite?.FindFloatingHost(this)?.Activate();
        }

        DockSite?.SetActiveWindow(this);
    }

    /// <summary>
    /// Floats the window out of the layout into its own top-level window. The window remembers
    /// where it was docked, so <see cref="Dock"/> puts it back in the same place.
    /// </summary>
    public void Float()
    {
        WhenPartOfADockSite(() =>
        {
            if (CanFloat && State is DockingWindowState.Docked && DockSite is { } site)
            {
                site.FloatWindow(this);
            }
        });
    }

    /// <summary>
    /// Collapses this window's container to the nearest auto-hide edge, like unpinning in
    /// Visual Studio. All windows sharing the container are auto-hidden together.
    /// </summary>
    public void AutoHide()
    {
        WhenPartOfADockSite(() =>
        {
            if (CanAutoHide && State == DockingWindowState.Docked && Container is { } container && DockSite is { } site)
            {
                LayoutManager.AutoHideContainer(site, container);
            }
        });
    }

    /// <summary>
    /// Docks the window back into the layout: an auto-hidden window pins its whole group back
    /// to the edge it was collapsed to, and a floating window returns to the position it was
    /// floated from, together with the other windows of its floating window.
    /// </summary>
    public void Dock()
    {
        WhenPartOfADockSite(() =>
        {
            if (DockSite is not { } site)
            {
                return;
            }

            if (State == DockingWindowState.AutoHide && site.FindAutoHideGroup(this) is { } group)
            {
                LayoutManager.DockAutoHideGroup(site, group);
            }
            else if (State == DockingWindowState.Floating && site.FindFloatingHost(this) is { } host)
            {
                LayoutManager.DockFloatingHost(site, host, DockTarget.Home);
            }
        });
    }

    // Runs a layout operation now, or as soon as this window becomes part of a dock site.
    //
    // Applications set up their initial layout from the point they have one, which is typically the
    // dock site's Loaded. A window declared in XAML is not yet attached to its site at that moment,
    // and dropping the call there would leave the panel where it was with nothing to show for it;
    // deferring means the operation still happens, one moment later.
    private void WhenPartOfADockSite(Action operation)
    {
        if (DockSite is not null)
        {
            operation();
            return;
        }

        pendingOperations += operation;
    }

    /// <summary>
    /// Closes the window, removing it from the layout. The close can be canceled by handlers of
    /// <see cref="DockSite.WindowClosing"/>. The window instance stays registered and can be reopened.
    /// </summary>
    public void Close()
    {
        if (!CanClose || !IsOpen)
        {
            return;
        }

        var site = DockSite;
        if (site is not null && !site.RaiseWindowClosing(this))
        {
            return;
        }

        LayoutManager.RemoveWindow(this);
        site?.NotifyWindowClosed(this);
    }

    // Called by the container when this window is added to it.
    internal void NotifyAttached()
    {
        IsOpen = true;
        State = DockingWindowState.Docked;

        if (IsRelocating)
        {
            return;
        }

        if (DockSite is { } site)
        {
            site.NotifyWindowOpened(this);
        }
        else
        {
            // The container is not in the visual tree yet (e.g. during XAML parse);
            // the notification is deferred until the window is loaded under a DockSite.
            pendingOpenedNotification = true;
        }
    }

    // Called by the container when this window is removed from it.
    internal void NotifyDetached()
    {
        if (IsRelocating)
        {
            return;
        }

        IsOpen = false;
        IsSelected = false;
        pendingOpenedNotification = false;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Inside a floating window there is no dock site above the window: the site it belongs
        // to is the one that owns the floating window, which its surface knows.
        if (this.FindSurface()?.Site is { } site)
        {
            DockSite = site;
            site.RegisterWindow(this);

            if (pendingOpenedNotification)
            {
                pendingOpenedNotification = false;
                site.NotifyWindowOpened(this);
            }

            var pending = pendingOperations;
            pendingOperations = null;
            pending?.Invoke();
        }
    }

    void IRelocatable.RaiseRelocated() => Relocated?.Invoke(this, EventArgs.Empty);
}
