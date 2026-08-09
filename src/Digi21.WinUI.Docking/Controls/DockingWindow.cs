using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Base class for windows that participate in a docking layout, such as <see cref="ToolWindow"/>.
/// The window's <see cref="ContentControl.Content"/> travels with it when it is re-docked.
/// </summary>
public abstract partial class DockingWindow : ContentControl
{
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
    public ToolWindowContainer? Container { get; internal set; }

    /// <summary>Gets the dock site this window belongs to, resolved when the window is first loaded.</summary>
    internal DockSite? DockSite { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the window is being moved to a new docking
    /// position. While set, attach/detach notifications do not raise opened/closed events.
    /// </summary>
    internal bool IsRelocating { get; set; }

    /// <summary>
    /// Selects the window in its container and makes it the active window of its dock site.
    /// If the window is auto-hidden, its flyout is opened.
    /// </summary>
    public void Activate()
    {
        if (this is ToolWindow tool)
        {
            if (State == DockingWindowState.AutoHide)
            {
                DockSite?.ShowAutoHideFlyout(tool);
                return;
            }

            Container?.Select(tool);

            if (State == DockingWindowState.Floating)
            {
                DockSite?.FindFloatingHost(tool)?.Activate();
            }
        }

        DockSite?.SetActiveWindow(this);
    }

    /// <summary>
    /// Floats the window out of the layout into its own top-level window. The window remembers
    /// where it was docked, so <see cref="Dock"/> puts it back in the same place.
    /// </summary>
    public void Float()
    {
        if (CanFloat && State is DockingWindowState.Docked && this is ToolWindow tool && DockSite is { } site)
        {
            site.FloatToolWindow(tool);
        }
    }

    /// <summary>
    /// Collapses this window's container to the nearest auto-hide edge, like unpinning in
    /// Visual Studio. All windows sharing the container are auto-hidden together.
    /// </summary>
    public void AutoHide()
    {
        if (CanAutoHide && State == DockingWindowState.Docked && Container is { } container && DockSite is { } site)
        {
            LayoutManager.AutoHideContainer(site, container);
        }
    }

    /// <summary>
    /// Docks the window back into the layout: an auto-hidden window pins its whole group back
    /// to the edge it was collapsed to, and a floating window returns to the position it was
    /// floated from, together with the other windows of its floating window.
    /// </summary>
    public void Dock()
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

    /// <summary>Called by the container when this window is added to it.</summary>
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

    /// <summary>Called by the container when this window is removed from it.</summary>
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
            if (this is ToolWindow tool)
            {
                site.RegisterWindow(tool);
            }

            if (pendingOpenedNotification)
            {
                pendingOpenedNotification = false;
                site.NotifyWindowOpened(this);
            }
        }
    }
}
