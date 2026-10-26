using System;
using Microsoft.UI.Dispatching;
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

    /// <summary>
    /// Raised when <see cref="IsContentVisible"/> changes: the window's content has just come on
    /// screen, or has just gone off it.
    /// </summary>
    /// <remarks>
    /// Content that costs something to keep running while nobody is looking — a video feed, a
    /// render loop, a subscription to a stream someone else has to produce — should start and stop
    /// on this event. <see cref="Relocated"/> answers a different question: it says the window
    /// moved, not whether it can be seen.
    /// </remarks>
    public event EventHandler? ContentVisibilityChanged;

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
        nameof(IsOpen), typeof(bool), typeof(DockingWindow), new PropertyMetadata(false, OnContentVisibilityInputChanged));

    /// <summary>Identifies the <see cref="IsActive"/> dependency property.</summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(DockingWindow), new PropertyMetadata(false));

    /// <summary>Identifies the <see cref="IsSelected"/> dependency property.</summary>
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(DockingWindow), new PropertyMetadata(false, OnContentVisibilityInputChanged));

    /// <summary>Identifies the <see cref="State"/> dependency property.</summary>
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(DockingWindowState), typeof(DockingWindow), new PropertyMetadata(DockingWindowState.Docked, OnContentVisibilityInputChanged));

    /// <summary>Identifies the <see cref="IsContentVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsContentVisibleProperty = DependencyProperty.Register(
        nameof(IsContentVisible), typeof(bool), typeof(DockingWindow), new PropertyMetadata(false, OnIsContentVisibleChanged));

    /// <summary>
    /// Whether the window's content is actually on screen right now.
    /// </summary>
    /// <remarks>
    /// It is not the same as being open. A window that shares a pane with others, docked or in a
    /// floating window, is only visible while its tab is the selected one, and an auto-hidden window
    /// only while its flyout is out. Both cases collapse the content without taking it out of the
    /// tree, so neither shows up as <see cref="FrameworkElement.Unloaded"/>.
    /// </remarks>
    public bool IsContentVisible
    {
        get => (bool)GetValue(IsContentVisibleProperty);
        private set => SetValue(IsContentVisibleProperty, value);
    }

    /// <summary>Identifies the <see cref="IsAttentionRequested"/> dependency property.</summary>
    public static readonly DependencyProperty IsAttentionRequestedProperty = DependencyProperty.Register(
        nameof(IsAttentionRequested), typeof(bool), typeof(DockingWindow), new PropertyMetadata(false));

    /// <summary>
    /// Gets a value indicating whether the window's tab is asking the user to look at it.
    /// </summary>
    /// <remarks>
    /// Set by <see cref="RequestAttention"/>, and cleared by the library once the content has come
    /// on screen, however that happens: the user selecting the tab, opening the window's auto-hide
    /// flyout, or the application activating the window. The library checks again before clearing
    /// it, so a window that only passes through a visible state while a docking operation moves it
    /// keeps its request, and an auto-hidden window's flyout has to stay out for half a second, so
    /// a pointer crossing the edge does not answer it. It is not part of a saved layout.
    /// </remarks>
    public bool IsAttentionRequested
    {
        get => (bool)GetValue(IsAttentionRequestedProperty);
        private set => SetValue(IsAttentionRequestedProperty, value);
    }

    /// <summary>
    /// Marks the window's tab as having something new to show, without bringing the window forward.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if a tab now carries the cue; <see langword="false"/> if the content
    /// is already on screen, where there is nothing to point at, or the window is closed and has no
    /// tab to point with.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Meant for content that is written to while nobody is looking, such as a log or a list of
    /// results, and that should say so without opening itself over whatever the user is doing. The
    /// tab's background pulses until the content is shown, and stays lit without pulsing when
    /// Windows has animation effects turned off or a high contrast theme is on.
    /// </para>
    /// <para>
    /// A closed window stays closed and remembers nothing: reopening it shows the content anyway.
    /// A tab drawn by a custom template shows the cue only if the template declares the
    /// <c>AttentionStates</c> group the default templates do.
    /// </para>
    /// <para>
    /// Like every member of a XAML element, it has to be called on the UI thread. Content written
    /// from a background thread asks through the window's <c>DispatcherQueue</c>.
    /// </para>
    /// </remarks>
    public bool RequestAttention()
    {
        if (!IsOpen || IsContentVisible)
        {
            return false;
        }

        IsAttentionRequested = true;
        return true;
    }

    /// <summary>Takes back an attention request the user has not answered yet.</summary>
    public void ClearAttentionRequest() => IsAttentionRequested = false;

    // Set by the DockSite as it shows and releases the flyout, which is the only thing that knows
    // whether an auto-hidden window is currently out.
    internal bool IsAutoHideFlyoutOpen
    {
        get => isAutoHideFlyoutOpen;
        set
        {
            if (isAutoHideFlyoutOpen == value)
            {
                return;
            }

            isAutoHideFlyoutOpen = value;
            RefreshContentVisibility();
        }
    }

    private bool isAutoHideFlyoutOpen;

    private static void OnContentVisibilityInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((DockingWindow)d).RefreshContentVisibility();

    private static void OnIsContentVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var window = (DockingWindow)d;

        if (window.IsContentVisible && window.IsAttentionRequested)
        {
            window.ClearAttentionOnceSettled();
        }

        window.ContentVisibilityChanged?.Invoke(d, EventArgs.Empty);
    }

    // Whatever the tab was pointing at is in front of the user, unless the window only passed through
    // a visible state on its way somewhere else: a pane being emptied selects each window in turn as
    // the others leave it, which is what unpinning a group or pinning it back does. So the answer
    // waits for the operation to finish, and the cue goes only if the content is still on screen.
    //
    // An auto-hidden window waits longer. Its flyout slides out as a preview the moment the pointer
    // touches its tab, so a pointer only crossing the edge on its way elsewhere would otherwise wipe
    // every cue it passed over without anyone having looked at a panel.
    private void ClearAttentionOnceSettled()
    {
        if (DispatcherQueue is not { } queue)
        {
            IsAttentionRequested = false;
            return;
        }

        if (State == DockingWindowState.AutoHide)
        {
            if (attentionTimer is null)
            {
                attentionTimer = queue.CreateTimer();
                attentionTimer.IsRepeating = false;
                attentionTimer.Tick += (_, _) => ClearAttentionIfStillVisible();
            }

            attentionTimer.Interval = AutoHideAttentionDwell;
            attentionTimer.Start();
            return;
        }

        if (!queue.TryEnqueue(DispatcherQueuePriority.Low, ClearAttentionIfStillVisible))
        {
            IsAttentionRequested = false;
        }
    }

    private void ClearAttentionIfStillVisible()
    {
        if (IsContentVisible)
        {
            IsAttentionRequested = false;
        }
    }

    // How long an auto-hidden window's flyout has to stay out before its request counts as answered.
    private static readonly TimeSpan AutoHideAttentionDwell = TimeSpan.FromMilliseconds(500);

    private DispatcherQueueTimer? attentionTimer;

    private void RefreshContentVisibility()
    {
        // A closed window has no tab left to carry the cue, and reopening it shows the content.
        if (!IsOpen)
        {
            IsAttentionRequested = false;
        }

        IsContentVisible = IsOpen && State switch
        {
            // Unpinned to an edge, it is only on screen while its flyout is out.
            DockingWindowState.AutoHide => IsAutoHideFlyoutOpen,

            // Docked, floating or a document: its pane collapses every window but the selected one.
            // A floating window is no exception, since it can hold a pane of several tabs.
            _ => IsSelected,
        };
    }

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

    /// <summary>
    /// Gets the pane that currently hosts this window as a tab, or <see langword="null"/> when
    /// there is none.
    /// </summary>
    /// <remarks>
    /// There is none in more cases than being closed, so this is the wrong thing to test to find
    /// out whether a window is in the layout — use <see cref="IsPlaced"/>. A window collapsed to an
    /// auto-hide edge has no container and is very much in the layout, while a window a load has
    /// taken out and not put back yet still has the one it came from, and that one is no longer
    /// part of anything.
    /// </remarks>
    public DockingWindowContainer? Container { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether the window is somewhere in the layout right now: in a pane,
    /// in a floating window, or collapsed to an auto-hide edge.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Open and placed are not the same thing, and the gap is what a docking call needs to know
    /// about: a window can only be docked beside or attached to one that is <em>placed</em>, since
    /// an unplaced one is nowhere for the new pane or tab to go.
    /// </para>
    /// <para>
    /// Neither is having a <see cref="Container"/> the same thing, in either direction. A window
    /// collapsed to an edge has none and is placed — <see cref="DockSite.AttachToolWindow"/> takes
    /// it as a target and joins the new window to its group — and a window still waiting to be
    /// rescued has one and is not.
    /// </para>
    /// <para>
    /// The gap opens while a layout is being loaded. A load rebuilds the layout tree first and puts
    /// the windows it does not mention back afterwards, one at a time, so a window still awaiting
    /// rescue is open, and is still holding the container it was in, and that container is no
    /// longer part of anything. That is the state every other window being rescued is in while
    /// <see cref="Serialization.DockSiteLayoutSerializer.UnresolvedWindowDocking"/> is raised for
    /// one of them. Test this and not <see cref="IsOpen"/> — nor the container itself — before
    /// docking against a window from that handler.
    /// </para>
    /// </remarks>
    public bool IsPlaced
    {
        get
        {
            if (DockSite is not { } site)
            {
                return false;
            }

            // Holding a container is not enough: the container has to still be part of the dock
            // site or of one of its floating windows. An abandoned one would take a tab and show
            // it nowhere.
            return (Container is { } container && LayoutTree.Locate(site, container) is not null)
                || site.FindAutoHideGroup(this) is not null;
        }
    }

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
            if (DockSite is { } site)
            {
                // The flyout opens in the dock site's window, which may sit behind a floating one.
                ScreenInterop.BringToForeground(site);
                site.ShowAutoHideFlyout(tool);
            }

            return;
        }

        Container?.Select(this);

        if (State == DockingWindowState.Floating)
        {
            DockSite?.FindFloatingHost(this)?.Activate();
        }
        else if (DockSite is { } dockedSite)
        {
            // A docked window can be activated while a floating window of the application holds
            // the foreground; its window has to come forward then, as a floating window's does.
            ScreenInterop.BringToForeground(dockedSite);
        }

        DockSite?.SetActiveWindow(this);
    }

    /// <summary>
    /// Floats the window out of the layout into its own top-level window. The window remembers
    /// where it was docked, so <see cref="Dock"/> puts it back in the same place.
    /// </summary>
    /// <remarks>
    /// Deferred while the window is not yet part of a dock site — see
    /// <see cref="AutoHide(AutoHideScope)"/>.
    /// </remarks>
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
    /// <remarks>
    /// Deferred while the window is not yet part of a dock site — see
    /// <see cref="AutoHide(AutoHideScope)"/>.
    /// </remarks>
    public void AutoHide() => AutoHide(AutoHideScope.Container);

    /// <summary>
    /// Collapses this window to the nearest auto-hide edge, taking its whole container with it or
    /// leaving the rest of the container docked, as the scope says.
    /// </summary>
    /// <param name="scope">How much of the layout goes to the edge.</param>
    /// <remarks>
    /// <para>
    /// <see cref="AutoHideScope.Window"/> is the programmatic way to unpin a single panel: the
    /// windows it shared a tab group with stay where they are, and pinning it back returns it to
    /// that group as a tab. Unpinning from the title bar stays a whole-container gesture, since a
    /// user dragging panels into one group means them to travel together.
    /// </para>
    /// <para>
    /// Only this window's <see cref="DockingWindow.CanAutoHide"/> is consulted for that scope. A
    /// whole container also needs every window in it to allow auto-hide, or none of them moves.
    /// </para>
    /// <para>
    /// Like <see cref="Float"/> and <see cref="Dock"/>, the call is deferred while the window is
    /// not yet part of a dock site, which is the case for a window declared in XAML during the
    /// dock site's own <c>Loaded</c>. It then runs when the window is loaded, so a layout saved
    /// immediately after the call still describes the arrangement the call was about to change.
    /// Save it from the window's own <c>Loaded</c>, or from a
    /// <c>DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, ...)</c> callback.
    /// </para>
    /// </remarks>
    public void AutoHide(AutoHideScope scope)
    {
        WhenPartOfADockSite(() =>
        {
            if (!CanAutoHide || State != DockingWindowState.Docked || Container is not { } container || DockSite is not { } site)
            {
                return;
            }

            if (scope == AutoHideScope.Window)
            {
                LayoutManager.AutoHideWindow(site, this);
            }
            else
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
    /// <remarks>
    /// Deferred while the window is not yet part of a dock site — see
    /// <see cref="AutoHide(AutoHideScope)"/>.
    /// </remarks>
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
                LayoutManager.DockAutoHideGroup(site, group, this as ToolWindow);
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
