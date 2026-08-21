using Digi21.WinUI.Docking.Interaction;
using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;

namespace Digi21.WinUI.Docking;

/// <summary>
/// The root control of a docking layout. Hosts a tree of docking containers
/// (such as <c>SplitContainer</c> and <c>ToolWindowContainer</c>) and the central workspace.
/// </summary>
[ContentProperty(Name = nameof(Child))]
public partial class DockSite : Control, IDockSurface
{
    /// <summary>Identifies the <see cref="Child"/> dependency property.</summary>
    public static readonly DependencyProperty ChildProperty = DependencyProperty.Register(
        nameof(Child),
        typeof(UIElement),
        typeof(DockSite),
        new PropertyMetadata(null));

    /// <summary>Identifies the <c>DockSite.RelativeSize</c> attached dependency property.</summary>
    public static readonly DependencyProperty RelativeSizeProperty = DependencyProperty.RegisterAttached(
        "RelativeSize",
        typeof(double),
        typeof(DockSite),
        new PropertyMetadata(1.0, OnRelativeSizeChanged));

    /// <summary>
    /// Gets the proportional share of space the element receives inside a <see cref="SplitContainer"/>.
    /// </summary>
    /// <param name="element">The element to read the value from.</param>
    public static double GetRelativeSize(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (double)element.GetValue(RelativeSizeProperty);
    }

    /// <summary>
    /// Sets the proportional share of space the element receives inside a <see cref="SplitContainer"/>.
    /// </summary>
    /// <param name="element">The element to set the value on.</param>
    /// <param name="value">The relative size. Values are proportions, not pixels; siblings share space by ratio.</param>
    public static void SetRelativeSize(UIElement element, double value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(RelativeSizeProperty, value);
    }

    private static void OnRelativeSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element && VisualTreeHelper.GetParent(element) is SplitContainer parent)
        {
            parent.InvalidateMeasure();
            parent.InvalidateArrange();
        }
    }

    /// <summary>Identifies the <see cref="ActiveWindow"/> dependency property.</summary>
    public static readonly DependencyProperty ActiveWindowProperty = DependencyProperty.Register(
        nameof(ActiveWindow),
        typeof(DockingWindow),
        typeof(DockSite),
        new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="ActiveDocument"/> dependency property.</summary>
    public static readonly DependencyProperty ActiveDocumentProperty = DependencyProperty.Register(
        nameof(ActiveDocument),
        typeof(DocumentWindow),
        typeof(DockSite),
        new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="AutoHideOpenTrigger"/> dependency property.</summary>
    public static readonly DependencyProperty AutoHideOpenTriggerProperty = DependencyProperty.Register(
        nameof(AutoHideOpenTrigger),
        typeof(AutoHideOpenTrigger),
        typeof(DockSite),
        new PropertyMetadata(Docking.AutoHideOpenTrigger.Pointer));

    /// <summary>Identifies the <see cref="IsAutoHideAnimated"/> dependency property.</summary>
    public static readonly DependencyProperty IsAutoHideAnimatedProperty = DependencyProperty.Register(
        nameof(IsAutoHideAnimated),
        typeof(bool),
        typeof(DockSite),
        new PropertyMetadata(true));

    /// <summary>Identifies the <see cref="AutoHideCloseDelay"/> dependency property.</summary>
    public static readonly DependencyProperty AutoHideCloseDelayProperty = DependencyProperty.Register(
        nameof(AutoHideCloseDelay),
        typeof(TimeSpan),
        typeof(DockSite),
        new PropertyMetadata(TimeSpan.FromMilliseconds(350)));

    private readonly List<ToolWindow> toolWindows = [];
    private readonly List<DocumentWindow> documents = [];
    private readonly List<AutoHideGroup> autoHideGroups = [];
    private readonly List<FloatingWindowHost> floatingHosts = [];
    private readonly Dictionary<DockSide, AutoHideTabStrip> autoHideStrips = [];
    private readonly HashSet<IRelocatable> pendingRelocations = [];
    private bool relocationFlushScheduled;
    private AutoHideOpenReason autoHideOpenReason;
    private bool autoHideCollapsing;
    private DispatcherQueueTimer? autoHideCollapseTimer;
    private AutoHideFlyout? autoHideFlyout;
    private ContentPresenter? layoutRootPresenter;
    private AppWindow? ownerWindow;
    private DockGuideOverlay? overlay;

    /// <summary>Initializes a new instance of the <see cref="DockSite"/> class.</summary>
    public DockSite()
    {
        DefaultStyleKey = typeof(DockSite);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");

        // The templates below reference the library's own brush and metric keys; this is what
        // puts them within reach of the application's resources.
        DockingThemeResources.Ensure();

        GotFocus += OnAnyDescendantGotFocus;
        Loaded += (_, _) => HookOwnerWindow();
        Unloaded += (_, _) => ReleaseOwnerWindow();

        // Registered once here rather than in OnApplyTemplate, which runs again every time the
        // template is re-applied and would stack the handler up; handledEventsToo, because
        // dismissing the flyout must see presses a control inside the layout already handled.
        AddHandler(PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnDismissPointerPressed), true);
    }

    // Watches the window hosting this dock site so its floating windows are closed while it is
    // still alive. They are owned windows, so leaving them to be destroyed together with their
    // owner tears down their XAML islands during the owner's own teardown, which crashes the
    // process. The dock site hooks itself when it loads, so a close the user asks for needs nothing
    // from the application.
    //
    // Closing only covers that user-initiated close. It does not arrive when the application calls
    // Window.Close() itself, and neither does anything else a control can reach in time: Destroying
    // and the owner's WM_DESTROY both land after the owned windows are already going, and XAML
    // raises no Unloaded on the dock site. Window.Closed is the last usable moment and only the
    // application holds it, which is why CloseFloatingWindows is public.
    private void HookOwnerWindow()
    {
        if (ownerWindow is not null || XamlRoot?.ContentIslandEnvironment is not { } environment)
        {
            return;
        }

        ownerWindow = AppWindow.GetFromWindowId(environment.AppWindowId);
        if (ownerWindow is not null)
        {
            ownerWindow.Closing += OnOwnerWindowClosing;
        }
    }

    // Closes the floating windows and lets go of the owner, so that a dock site taken out of the
    // tree leaves nothing of itself behind on a window that outlives it.
    //
    // A dock site moved within the tree arrives here too, after the Loaded of its new place has
    // already run (WinUI queues Unloaded), and that Loaded could not hook anything because the old
    // hook was still in place. Tearing down unconditionally would therefore close the floating
    // windows of a site that is still showing and drop the owner hook with no Loaded left to
    // restore it — floating windows opened afterwards would outlive the owner's Closing and crash
    // its teardown. IsLoaded tells the two apart: it is false for a site that actually left.
    private void ReleaseOwnerWindow()
    {
        if (IsLoaded && ownerWindow is not null
            && XamlRoot?.ContentIslandEnvironment is { } environment
            && ownerWindow.Id.Value == environment.AppWindowId.Value)
        {
            // Moved within the same window: the hook and the floating windows still hold.
            return;
        }

        CloseFloatingWindows();

        if (ownerWindow is not null)
        {
            ownerWindow.Closing -= OnOwnerWindowClosing;
            ownerWindow = null;
        }

        // Moved into another top-level window: watch that one from now on.
        if (IsLoaded)
        {
            HookOwnerWindow();
        }
    }

    private void OnOwnerWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // An application that stops the close here, to ask about unsaved work, must not find its
        // floating windows closed regardless. It gets to decide first: a handler added when the
        // window was built runs before this one, which the dock site only adds when it loads.
        if (args.Cancel)
        {
            return;
        }

        CloseFloatingWindows();
    }

    /// <summary>Raised when a window becomes the active window of this dock site.</summary>
    public event EventHandler<DockingWindowEventArgs>? WindowActivated;

    /// <summary>Raised when a window stops being the active window of this dock site.</summary>
    public event EventHandler<DockingWindowEventArgs>? WindowDeactivated;

    /// <summary>Raised when a window is added to the layout.</summary>
    public event EventHandler<DockingWindowEventArgs>? WindowOpened;

    /// <summary>Raised before a window closes. Set <see cref="DockingWindowClosingEventArgs.Cancel"/> to keep it open.</summary>
    public event EventHandler<DockingWindowClosingEventArgs>? WindowClosing;

    /// <summary>Raised after a window has been removed from the layout.</summary>
    public event EventHandler<DockingWindowEventArgs>? WindowClosed;

    /// <summary>Raised whenever the docking layout structure changes.</summary>
    public event EventHandler<LayoutChangedEventArgs>? LayoutChanged;

    /// <summary>
    /// Raised just before a document tab shows its context menu, with the entries the library
    /// provides already in <see cref="DocumentTabContextMenuEventArgs.Items"/>, so an application
    /// can add its own commands, reorder them or replace the menu.
    /// </summary>
    public event EventHandler<DocumentTabContextMenuEventArgs>? DocumentTabContextMenuOpening;

    /// <summary>
    /// Gets or sets the root element of the docking layout tree.
    /// </summary>
    public UIElement? Child
    {
        get => (UIElement?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    /// <summary>
    /// Gets or sets how long an auto-hide flyout opened by pointing at its tab waits before
    /// sliding back once the pointer leaves it. The delay is a cushion for the pointer crossing
    /// outside the panel on its way to a control near the edge; returning within it keeps the
    /// panel open. It does not apply to a flyout the user opened by clicking or has typed into,
    /// which stays until the focus moves elsewhere. Defaults to 350 milliseconds.
    /// </summary>
    public TimeSpan AutoHideCloseDelay
    {
        get => (TimeSpan)GetValue(AutoHideCloseDelayProperty);
        set => SetValue(AutoHideCloseDelayProperty, value);
    }

    /// <summary>
    /// Gets or sets what it takes for an auto-hidden panel to slide out: pointing at its tab, which
    /// is the default, or clicking it.
    /// </summary>
    /// <remarks>
    /// <see cref="Docking.AutoHideOpenTrigger.Click"/> is for an application whose edges the
    /// pointer crosses on its way to something else, and for users who would rather nothing moved
    /// until they asked. It governs the pointer and nothing else: clicking a tab, activating a
    /// window from code and selecting a tab through UI Automation open the panel either way.
    /// </remarks>
    public AutoHideOpenTrigger AutoHideOpenTrigger
    {
        get => (AutoHideOpenTrigger)GetValue(AutoHideOpenTriggerProperty);
        set => SetValue(AutoHideOpenTriggerProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether an auto-hidden panel slides out from its edge
    /// instead of appearing at once. Enabled by default.
    /// </summary>
    /// <remarks>
    /// Windows has the last word: with animation effects turned off — in Settings, or by battery
    /// saver, or because the application is running over a remote session — the panel appears at
    /// once whatever this says. The slide is a render transform, so the panel's content is laid out
    /// once, at its final size, and is on screen and in the automation tree from the first frame
    /// rather than at the end of the animation. Its duration is the
    /// <c>DockingAutoHideSlideMilliseconds</c> theme resource.
    /// </remarks>
    public bool IsAutoHideAnimated
    {
        get => (bool)GetValue(IsAutoHideAnimatedProperty);
        set => SetValue(IsAutoHideAnimatedProperty, value);
    }

    /// <summary>Gets the currently active window, or <see langword="null"/> when none is active.</summary>
    public DockingWindow? ActiveWindow
    {
        get => (DockingWindow?)GetValue(ActiveWindowProperty);
        private set => SetValue(ActiveWindowProperty, value);
    }

    /// <summary>
    /// Gets the active document, which stays the last activated one while a tool window has the
    /// focus, or <see langword="null"/> when no document has been activated yet.
    /// </summary>
    public DocumentWindow? ActiveDocument
    {
        get => (DocumentWindow?)GetValue(ActiveDocumentProperty);
        private set => SetValue(ActiveDocumentProperty, value);
    }

    /// <summary>
    /// Gets all tool windows known to this dock site, including closed ones that can be reopened.
    /// </summary>
    public IReadOnlyList<ToolWindow> ToolWindows
    {
        get
        {
            EnsureWindowsRegistered();
            return toolWindows;
        }
    }

    /// <summary>
    /// Gets all documents known to this dock site, including closed ones that can be reopened.
    /// </summary>
    public IReadOnlyList<DocumentWindow> Documents
    {
        get
        {
            EnsureWindowsRegistered();
            return documents;
        }
    }

    /// <summary>
    /// Gets the document area of the layout, or <see langword="null"/> when the layout has none.
    /// </summary>
    public DocumentHost? DocumentHost => LayoutTree.DocumentHosts(Child).FirstOrDefault();

    // Gets the controller that runs interactive drag-and-drop re-docking, once the template is
    // applied.
    internal DragDockController? DragController { get; private set; }

    // Gets the presenter that hosts the docked layout, used as the coordinate reference for the
    // edge strips.
    internal ContentPresenter? LayoutRoot => layoutRootPresenter;

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        autoHideStrips.Clear();
        foreach (var (name, side) in new[]
        {
            ("PART_AutoHideStripLeft", DockSide.Left),
            ("PART_AutoHideStripTop", DockSide.Top),
            ("PART_AutoHideStripRight", DockSide.Right),
            ("PART_AutoHideStripBottom", DockSide.Bottom),
        })
        {
            if (GetTemplateChild(name) is AutoHideTabStrip strip)
            {
                autoHideStrips[side] = strip;
            }
        }

        autoHideFlyout = GetTemplateChild("PART_AutoHideFlyout") as AutoHideFlyout;

        if (layoutRootPresenter is not null)
        {
            layoutRootPresenter.SizeChanged -= OnLayoutRootSizeChanged;
        }

        layoutRootPresenter = GetTemplateChild("PART_LayoutRoot") as ContentPresenter;

        if (layoutRootPresenter is not null)
        {
            layoutRootPresenter.SizeChanged += OnLayoutRootSizeChanged;
        }

        RefreshAutoHideStrips();

        if (GetTemplateChild("PART_DockPreview") is Rectangle preview
            && GetTemplateChild("PART_CenterGuides") is DockGuidePanel centerGuides
            && GetTemplateChild("PART_EdgeGuideLeft") is DockGuide left
            && GetTemplateChild("PART_EdgeGuideTop") is DockGuide top
            && GetTemplateChild("PART_EdgeGuideRight") is DockGuide right
            && GetTemplateChild("PART_EdgeGuideBottom") is DockGuide bottom)
        {
            overlay = new DockGuideOverlay(
                this,
                preview,
                GetTemplateChild("PART_DragGhost") as Border,
                GetTemplateChild("PART_DragGhostText") as TextBlock,
                centerGuides,
                new Dictionary<DockSide, DockGuide>
                {
                    [DockSide.Left] = left,
                    [DockSide.Top] = top,
                    [DockSide.Right] = right,
                    [DockSide.Bottom] = bottom,
                });

            DragController = new DragDockController(this);
        }
        else
        {
            overlay = null;
            DragController = null;
        }
    }

    DockSite IDockSurface.Site => this;

    FrameworkElement IDockSurface.Root => this;

    bool IDockSurface.IsFloating => false;

    UIElement? ILayoutHost.LayoutChild
    {
        get => Child;
        set => Child = value;
    }

    DockGuideOverlay? IDockSurface.Overlay => overlay;

    void IDockSurface.OnLayoutMutated()
    {
    }

    /// <summary>
    /// Docks a tool window as a new pane at an edge of this dock site. Also reopens closed windows.
    /// </summary>
    /// <param name="window">The window to dock.</param>
    /// <param name="side">The dock site edge to dock to.</param>
    public void DockToolWindow(ToolWindow window, DockSide side)
    {
        ArgumentNullException.ThrowIfNull(window);
        LayoutManager.DockToSide(this, window, side);
    }

    /// <summary>
    /// Docks a tool window as a new pane beside the container of another window.
    /// </summary>
    /// <param name="window">The window to dock.</param>
    /// <param name="target">A window that is placed in the layout, whose container is the dock target.</param>
    /// <param name="side">The side of the target to dock to.</param>
    /// <exception cref="InvalidOperationException">
    /// The target has no container to dock beside: it is closed, it is collapsed to an auto-hide
    /// edge, or it is a window a layout being loaded has not put back yet. Test
    /// <see cref="DockingWindow.IsPlaced"/> — not <see cref="DockingWindow.IsOpen"/> — before
    /// docking against a window whose state is not yours to know.
    /// </exception>
    public void DockToolWindow(ToolWindow window, DockingWindow target, DockSide side)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(target);

        if (PaneOf(target) is not { } container)
        {
            throw new InvalidOperationException(
                FindAutoHideGroup(target) is not null
                    ? "The target window is collapsed to an auto-hide edge, so it has no pane to dock beside. Attach the window to it instead, or pin the target back first."
                    : "The target window is not placed in the layout, so there is no container to dock beside.");
        }

        LayoutManager.DockRelativeTo(this, window, container, side);
    }

    /// <summary>
    /// Attaches a tool window as a new tab beside another window, wherever that window is: in its
    /// container, or in its group if the group is collapsed to an auto-hide edge.
    /// </summary>
    /// <param name="window">The window to attach.</param>
    /// <param name="target">A window that is placed in the layout, whose group receives the new tab.</param>
    /// <remarks>
    /// Attaching to a collapsed group leaves the new window collapsed with it: it becomes a tab of
    /// the same edge group, opens in the same flyout, and is pinned back into the layout with the
    /// rest of it. Asking for the group a window belongs with is a question the group's state does
    /// not change the answer to.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The target is not placed in the layout: it is closed, or it is a window a layout being
    /// loaded has not put back yet. Test <see cref="DockingWindow.IsPlaced"/> — not
    /// <see cref="DockingWindow.IsOpen"/> — before attaching to a window whose state is not yours
    /// to know.
    /// </exception>
    public void AttachToolWindow(ToolWindow window, DockingWindow target)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(target);

        if (PaneOf(target) is { } container)
        {
            LayoutManager.AttachAsTab(this, window, container);
            return;
        }

        if (FindAutoHideGroup(target) is { } group)
        {
            LayoutManager.AttachToAutoHideGroup(this, window, group);
            return;
        }

        throw new InvalidOperationException("The target window is not placed in the layout, so there is nothing to attach it to.");
    }

    // The pane a window is in, and only when that pane is still part of the layout. A window a load
    // has taken out and not put back yet still holds the container it was in, and inserting a tab
    // into that one would show it nowhere: refusing is what turns a panel that silently disappears
    // into a mistake the caller can see.
    private DockingWindowContainer? PaneOf(DockingWindow target)
    {
        return target.Container is { } container && LayoutTree.Locate(this, container) is not null
            ? container
            : null;
    }

    /// <summary>
    /// Opens a document in the document area, as a new tab of its active group. Also reopens
    /// closed documents.
    /// </summary>
    /// <param name="document">The document to open.</param>
    /// <exception cref="InvalidOperationException">The layout has no <see cref="DocumentHost"/>.</exception>
    public void OpenDocument(DocumentWindow document) => OpenDocument(document, provisional: false);

    /// <summary>
    /// Opens a document in the document area, optionally as the provisional (preview) document of
    /// its active group.
    /// </summary>
    /// <param name="document">The document to open.</param>
    /// <param name="provisional">
    /// <see langword="true"/> to open it in preview, which closes the document the group was
    /// previewing; <see langword="false"/> to open an ordinary tab.
    /// </param>
    /// <exception cref="InvalidOperationException">The layout has no <see cref="DocumentHost"/>.</exception>
    public void OpenDocument(DocumentWindow document, bool provisional)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (DocumentHost is not { } host)
        {
            throw new InvalidOperationException("The layout of this dock site has no DocumentHost to open documents in.");
        }

        LayoutManager.OpenDocument(this, host, document, provisional);
    }

    /// <summary>
    /// Floats a window out of the layout into its own top-level window, placed near the dock
    /// site. Floating windows can be moved across monitors and dragged back into the layout.
    /// </summary>
    /// <param name="window">The window to float.</param>
    public void FloatWindow(DockingWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        FloatWindow(window, DefaultFloatingBounds(window));
    }

    /// <summary>
    /// Floats a window out of the layout into its own top-level window with explicit bounds.
    /// </summary>
    /// <param name="window">The window to float.</param>
    /// <param name="bounds">The bounds of the floating window, in screen pixels.</param>
    public void FloatWindow(DockingWindow window, RectInt32 bounds)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!window.CanFloat)
        {
            return;
        }

        LayoutManager.FloatWindow(this, window, FloatingWindowHost.ClampToDisplay(bounds));
    }

    // Gets the floating windows opened by this dock site.
    internal IReadOnlyList<FloatingWindowHost> FloatingHosts => floatingHosts;

    internal void AddFloatingHost(FloatingWindowHost host) => floatingHosts.Add(host);

    internal void RemoveFloatingHost(FloatingWindowHost host) => floatingHosts.Remove(host);

    // Finds the floating window hosting the given window, if any.
    internal FloatingWindowHost? FindFloatingHost(DockingWindow window)
    {
        return floatingHosts.FirstOrDefault(h => h.Windows.Contains(window));
    }

    // Finds the floating window with the given window handle, if it belongs to this site.
    internal FloatingWindowHost? FindFloatingHost(IntPtr handle)
    {
        return handle == IntPtr.Zero ? null : floatingHosts.FirstOrDefault(h => h.Handle == handle);
    }

    /// <summary>
    /// Closes the floating windows opened from this dock site, releasing the windows they host.
    /// Those windows stay registered, so a layout load can bring them back.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An application needs this in one case only: when it closes the window hosting the dock site
    /// from code, as a File &gt; Exit command does. A floating window is owned by that window, and
    /// leaving it to be destroyed alongside its owner tears down its XAML island during the owner's
    /// own teardown, which takes the process down with no managed exception to catch.
    /// </para>
    /// <para>
    /// A close the user asks for — the caption button, Alt+F4 — is handled by the dock site itself.
    /// A close the application asks for is not: WinUI raises no event on <c>AppWindow</c> for it,
    /// and a control has no supported way of reaching the <c>Window</c> that would raise
    /// <c>Closed</c>. Hence this call, from the window's own handler:
    /// </para>
    /// <code>
    /// Closed += (_, _) => DockSite.CloseFloatingWindows();
    /// </code>
    /// </remarks>
    public void CloseFloatingWindows()
    {
        foreach (var host in floatingHosts.ToList())
        {
            host.ReleaseAndClose();
        }
    }

    // Picks the initial bounds of a floating window: the size it has while docked, cascaded from
    // the site's corner.
    private RectInt32 DefaultFloatingBounds(DockingWindow window)
    {
        var (width, height) = FloatingWindowHost.PreferredSize(window.Container);
        var size = ScreenInterop.ToScreenSize(this, width, height);
        var cascade = 24.0 * (floatingHosts.Count + 1);
        var origin = ScreenInterop.ToScreen(this, new Windows.Foundation.Point(cascade, cascade))
            ?? new Windows.Graphics.PointInt32(120, 120);

        return new RectInt32(origin.X, origin.Y, size.Width, size.Height);
    }

    // Gets the auto-hide groups collapsed to the edges of this dock site.
    internal IReadOnlyList<AutoHideGroup> AutoHideGroups => autoHideGroups;

    // Finds the auto-hide group containing the given window, if any.
    internal AutoHideGroup? FindAutoHideGroup(DockingWindow window)
    {
        return window is ToolWindow tool ? autoHideGroups.FirstOrDefault(g => g.Windows.Contains(tool)) : null;
    }

    internal void AddAutoHideGroup(AutoHideGroup group)
    {
        autoHideGroups.Add(group);
        RefreshAutoHideStrips();
    }

    internal void RemoveAutoHideGroup(AutoHideGroup group)
    {
        if (autoHideFlyout?.Window is { } shown && group.Windows.Contains(shown))
        {
            HideAutoHideFlyout();
        }

        autoHideGroups.Remove(group);
        RefreshAutoHideStrips();
    }

    internal void ClearAutoHideGroups()
    {
        HideAutoHideFlyout();
        autoHideGroups.Clear();
        RefreshAutoHideStrips();
    }

    // Synchronizes the edge tab strips with the current auto-hide groups.
    internal void RefreshAutoHideStrips()
    {
        foreach (var (side, strip) in autoHideStrips)
        {
            var groups = autoHideGroups.Where(g => g.Edge == side && g.Windows.Count > 0).ToList();
            strip.SetGroups(groups);
            strip.Visibility = groups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // The auto-hidden window whose panel is currently out, if any.
    internal ToolWindow? AutoHideFlyoutWindow =>
        autoHideFlyout is { Visibility: Visibility.Visible } flyout ? flyout.Window : null;

    // Shows the auto-hide flyout for the given window, as if its tab had been clicked.
    internal void ShowAutoHideFlyout(ToolWindow window)
    {
        ShowAutoHideFlyout(window, AutoHideOpenReason.Activation);
    }

    // Shows the auto-hide flyout for the given window. How it was opened decides how it closes
    // later, so it is recorded here and nowhere else.
    internal void ShowAutoHideFlyout(ToolWindow window, AutoHideOpenReason reason)
    {
        if (!AutoHideOpenPolicy.ShouldOpen(AutoHideOpenTrigger, reason))
        {
            return;
        }

        if (autoHideFlyout is null || layoutRootPresenter is null
            || FindAutoHideGroup(window) is not { } group)
        {
            return;
        }

        // The pointer crossing the tab strip must not take away the panel the user is working in:
        // a preview replaces a preview, but only a click swaps out a panel that was opened for
        // real. This is the same protection the dismissal rule gives, applied to the way in.
        if (reason == AutoHideOpenReason.Hover
            && autoHideFlyout.Window is { } current
            && !ReferenceEquals(current, window)
            && autoHideOpenReason == AutoHideOpenReason.Activation)
        {
            return;
        }

        CancelAutoHideCollapse();

        // A panel asked for again while it was sliding away comes straight back. It was never
        // released, so it is still the one the flyout is showing, and the fast path below applies.
        CancelAutoHideSlide();

        if (ReferenceEquals(autoHideFlyout.Window, window))
        {
            // Already showing this window. Re-rooting it would raise Loaded, Unloaded and a
            // spurious Relocated — the signal that tells content like a WebView2 to rebuild — on
            // every click of its tab or its title bar, for a window that never moved.
            //
            // Clicking the tab of a panel that is merely being previewed does upgrade it: the user
            // asked for it this time, so it no longer goes away when the pointer wanders off.
            if (reason == AutoHideOpenReason.Activation)
            {
                autoHideOpenReason = AutoHideOpenReason.Activation;
                SetActiveWindow(window);
            }

            return;
        }

        // Showing the flyout takes the window out of the auto-hide group and into the flyout's own
        // content host, which is a move like any other as far as its content is concerned.
        autoHideFlyout.Show(window);
        NotifyRelocated(window);

        PositionAutoHideFlyout(group);
        autoHideFlyout.Visibility = Visibility.Visible;
        autoHideOpenReason = reason;

        // Only a panel that has just come out slides. Repositioning one that is already showing —
        // which happens whenever the area under it is resized — must not replay the animation.
        if (AutoHideSlideDuration is { Ticks: > 0 } duration)
        {
            autoHideFlyout.SlideIn(group.Edge, duration);
        }

        // Pointing at a tab shows the panel without disturbing what the user is working in: making
        // it the active window would move the focus, fire WindowActivated, and take the previous
        // window's title bar out of its active state, all for a preview the pointer dismisses.
        if (reason == AutoHideOpenReason.Activation)
        {
            SetActiveWindow(window);
        }
    }

    // How long an auto-hidden panel takes to slide, or zero when it must not slide at all: the
    // application turned the animation off, the theme set it to nothing, or Windows has animation
    // effects off — which the application does not get to overrule.
    private TimeSpan AutoHideSlideDuration
    {
        get
        {
            if (!IsAutoHideAnimated || !ScreenInterop.SystemAnimationsEnabled)
            {
                return TimeSpan.Zero;
            }

            var milliseconds = DockingThemeResources.Value("DockingAutoHideSlideMilliseconds", 150.0);

            return milliseconds > 0 ? TimeSpan.FromMilliseconds(milliseconds) : TimeSpan.Zero;
        }
    }

    // Puts the panel away the way the user sees it go: sliding back into the edge it came out of,
    // and released only once it is out of sight.
    //
    // Only the dismissal rule comes through here. Everything else that closes the flyout — a layout
    // being loaded, a window being closed or re-docked, another panel coming out — calls
    // HideAutoHideFlyout, which cancels the slide and does the work at once. An animation is a
    // courtesy to the eye; nothing that needs the window back may wait for one.
    private void CollapseAutoHideFlyout()
    {
        if (autoHideCollapsing)
        {
            // Already on its way out. A second trigger must not restart the slide, which would look
            // like the panel jumping back out to leave again.
            return;
        }

        if (autoHideFlyout?.Window is not { } shown
            || AutoHideSlideDuration is not { Ticks: > 0 } duration
            || FindAutoHideGroup(shown) is not { } group
            || !autoHideFlyout.SlideOut(group.Edge, duration, HideAutoHideFlyout))
        {
            HideAutoHideFlyout();
            return;
        }

        autoHideCollapsing = true;
    }

    // Abandons a collapse that is under way, leaving the panel on screen where it is. The panel was
    // never released, so there is nothing to bring back.
    private void CancelAutoHideSlide()
    {
        if (autoHideCollapsing)
        {
            autoHideCollapsing = false;
            autoHideFlyout?.CancelSlide();
        }
    }

    // Called by the flyout and the auto-hide tabs when the pointer arrives. Anything the pointer
    // can reach on its way between the tab and the panel counts as staying in.
    internal void NotifyAutoHidePointerEntered()
    {
        CancelAutoHideCollapse();

        // The pointer came back while the panel was sliding away. It stays, and it stays where it
        // is rather than sliding back out from an edge it never reached.
        CancelAutoHideSlide();
    }

    // Called when the pointer leaves the flyout or a tab. The move from one to the other raises an
    // exit before the matching entry, so this only ever schedules: the delay is what tells a
    // crossing apart from a departure.
    internal void NotifyAutoHidePointerExited()
    {
        if (autoHideFlyout?.Window is null)
        {
            return;
        }

        var delay = AutoHideCloseDelay;
        if (delay <= TimeSpan.Zero)
        {
            TryCollapseAutoHideFlyout(AutoHideDismissTrigger.PointerLeft);
            return;
        }

        if (DispatcherQueue is null)
        {
            return;
        }

        autoHideCollapseTimer ??= CreateAutoHideCollapseTimer();
        autoHideCollapseTimer.Interval = delay;
        autoHideCollapseTimer.Start();
    }

    private DispatcherQueueTimer CreateAutoHideCollapseTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.IsRepeating = false;
        timer.Tick += (_, _) => TryCollapseAutoHideFlyout(AutoHideDismissTrigger.PointerLeft);
        return timer;
    }

    private void CancelAutoHideCollapse()
    {
        autoHideCollapseTimer?.Stop();
    }

    // Collapses the flyout when the policy says this trigger ends its stay.
    private void TryCollapseAutoHideFlyout(AutoHideDismissTrigger trigger)
    {
        CancelAutoHideCollapse();

        if (autoHideFlyout?.Window is null)
        {
            return;
        }

        if (AutoHideDismissPolicy.ShouldCollapse(autoHideOpenReason, trigger, IsFocusInsideAutoHideFlyout()))
        {
            CollapseAutoHideFlyout();
        }
    }

    // Whether the focused element is inside the open flyout. Read from the focus manager rather
    // than tracked across events, so it is right even when the focus moved without passing through
    // this dock site.
    private bool IsFocusInsideAutoHideFlyout()
    {
        if (autoHideFlyout is null || XamlRoot is null)
        {
            return false;
        }

        return FocusManager.GetFocusedElement(XamlRoot) is DependencyObject focused
            && IsInsideAutoHideFlyout(focused);
    }

    private bool IsInsideAutoHideFlyout(DependencyObject element)
    {
        return autoHideFlyout is not null
            && (ReferenceEquals(element, autoHideFlyout) || ReferenceEquals(element.FindAncestor<AutoHideFlyout>(), autoHideFlyout));
    }

    private static bool IsInsideAutoHideStrip(DependencyObject element)
    {
        return element is AutoHideTabStrip or AutoHideTabItem || element.FindAncestor<AutoHideTabStrip>() is not null;
    }

    // Sizes the flyout to the area the layout occupies and pins it to its group's edge. The flyout
    // is laid out on a canvas over the whole dock site, so unlike the rest of the chrome it does
    // not follow that area on its own: this is called again whenever the area changes size, or
    // maximizing the window would leave the flyout hanging where it was.
    private void PositionAutoHideFlyout(AutoHideGroup group)
    {
        if (autoHideFlyout is null || layoutRootPresenter is null)
        {
            return;
        }

        var cellWidth = layoutRootPresenter.ActualWidth;
        var cellHeight = layoutRootPresenter.ActualHeight;
        var horizontal = group.Edge is DockSide.Left or DockSide.Right;
        var size = Math.Min(group.Size, (horizontal ? cellWidth : cellHeight) * 0.8);

        var (width, height) = horizontal ? (size, cellHeight) : (cellWidth, size);

        autoHideFlyout.Width = width;
        autoHideFlyout.Height = height;

        var origin = layoutRootPresenter
            .TransformToVisual(this)
            .TransformPoint(new Windows.Foundation.Point(0, 0));
        Canvas.SetLeft(autoHideFlyout, origin.X + (group.Edge == DockSide.Right ? cellWidth - width : 0));
        Canvas.SetTop(autoHideFlyout, origin.Y + (group.Edge == DockSide.Bottom ? cellHeight - height : 0));
    }

    private void OnLayoutRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (autoHideFlyout?.Window is { } shown && FindAutoHideGroup(shown) is { } group)
        {
            PositionAutoHideFlyout(group);
        }
    }

    // Hides the auto-hide flyout when it is the one showing the given window, and leaves it alone
    // when it is closed or showing some other window.
    internal void HideAutoHideFlyoutFor(ToolWindow window)
    {
        if (autoHideFlyout?.Window is { } shown && ReferenceEquals(shown, window))
        {
            HideAutoHideFlyout();
        }
    }

    // Hides the auto-hide flyout if it is open, at once and without ceremony. This is what puts the
    // window back in its group, so it is also the end of an animated collapse.
    internal void HideAutoHideFlyout()
    {
        CancelAutoHideCollapse();

        // Whatever the flyout was doing, it is over. Called from the end of a slide, this is the
        // tidy-up of the very slide that called it; called from anywhere else, it is what stops one
        // in its tracks.
        autoHideCollapsing = false;
        autoHideFlyout?.CancelSlide();

        if (autoHideFlyout?.Window is { } shown)
        {
            if (ReferenceEquals(ActiveWindow, shown))
            {
                SetActiveWindow(null);
            }

            autoHideFlyout.Release();
        }

        if (autoHideFlyout is not null)
        {
            autoHideFlyout.Visibility = Visibility.Collapsed;
        }
    }

    private void OnDismissPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (autoHideFlyout?.Window is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this).Position;
        if (IsWithin(autoHideFlyout, point))
        {
            // Clicking inside settles it: a panel that was only being previewed is now the one the
            // user is working in, even if what they clicked takes no focus.
            autoHideOpenReason = AutoHideOpenReason.Activation;
            CancelAutoHideCollapse();
            return;
        }

        foreach (var strip in autoHideStrips.Values)
        {
            if (strip.Visibility == Visibility.Visible && IsWithin(strip, point))
            {
                return;
            }
        }

        // Decided one turn later, on purpose. Whether this press ends the flyout's stay depends on
        // where it leaves the focus, and XAML moves the focus after the press is delivered:
        // checking now would read the focus the click is about to take away. If the press turns out
        // to move the focus out of the flyout, the focus handler collapses it first and this
        // becomes a no-op.
        DispatcherQueue?.TryEnqueue(
            DispatcherQueuePriority.Low,
            () => TryCollapseAutoHideFlyout(AutoHideDismissTrigger.PointerPressedOutside));

        bool IsWithin(FrameworkElement element, Windows.Foundation.Point p)
        {
            return element
                .TransformToVisual(this)
                .TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight))
                .Contains(p);
        }
    }

    /// <summary>Selects and activates the given window. Equivalent to <see cref="DockingWindow.Activate"/>.</summary>
    /// <param name="window">The window to activate.</param>
    public void ActivateWindow(DockingWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.Activate();
    }

    /// <summary>Closes the given window. Equivalent to <see cref="DockingWindow.Close"/>.</summary>
    /// <param name="window">The window to close.</param>
    public void CloseWindow(DockingWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.Close();
    }

    // Takes into the registry every window this dock site already holds: in its layout, inside its
    // floating windows, and collapsed to its edges.
    //
    // A window declared in XAML registers itself when it loads, and a dock site's own Loaded is
    // raised before that of the windows it contains. An application restoring its layout from
    // there — which is where an application has a dock site to restore into — would otherwise find
    // an empty registry, so no serialized id would match anything and the load would empty the
    // window instead of filling it.
    private void EnsureWindowsRegistered()
    {
        foreach (var window in LayoutTree.Windows(Child))
        {
            Adopt(window);
        }

        foreach (var host in floatingHosts)
        {
            foreach (var window in host.Windows)
            {
                Adopt(window);
            }
        }

        foreach (var group in autoHideGroups)
        {
            foreach (var window in group.Windows)
            {
                Adopt(window);
            }
        }

        void Adopt(DockingWindow window)
        {
            window.DockSite = this;
            RegisterWindow(window);
        }
    }

    // Adds a window to the registry of known windows.
    internal void RegisterWindow(DockingWindow window)
    {
        switch (window)
        {
            case ToolWindow tool when !toolWindows.Contains(tool):
                toolWindows.Add(tool);
                break;
            case DocumentWindow document when !documents.Contains(document):
                documents.Add(document);
                break;
        }
    }

    // Makes the given window the active one, deactivating the previous active window.
    internal void SetActiveWindow(DockingWindow? window)
    {
        var previous = ActiveWindow;
        if (ReferenceEquals(previous, window))
        {
            return;
        }

        ActiveWindow = window;

        if (window is DocumentWindow document)
        {
            ActiveDocument = document;
        }

        // A floating window is named after the window it is showing, which is the active one while
        // that window is hosted in it. Only now is it known.
        foreach (var host in floatingHosts)
        {
            host.RefreshCaption();
        }

        if (previous is not null)
        {
            previous.IsActive = false;
            WindowDeactivated?.Invoke(this, new DockingWindowEventArgs(previous));
        }

        if (window is not null)
        {
            window.IsActive = true;
            WindowActivated?.Invoke(this, new DockingWindowEventArgs(window));
        }
    }

    internal void NotifyWindowOpened(DockingWindow window)
    {
        WindowOpened?.Invoke(this, new DockingWindowEventArgs(window));
        LayoutChanged?.Invoke(this, new LayoutChangedEventArgs(LayoutChangeKind.WindowOpened));
    }

    // Raises WindowClosing and returns whether the close may proceed.
    internal bool RaiseWindowClosing(DockingWindow window)
    {
        var args = new DockingWindowClosingEventArgs(window);
        WindowClosing?.Invoke(this, args);
        return !args.Cancel;
    }

    internal void NotifyWindowClosed(DockingWindow window)
    {
        if (ReferenceEquals(ActiveWindow, window))
        {
            SetActiveWindow(null);
        }

        if (ReferenceEquals(ActiveDocument, window))
        {
            ActiveDocument = null;
        }

        WindowClosed?.Invoke(this, new DockingWindowEventArgs(window));
        LayoutChanged?.Invoke(this, new LayoutChangedEventArgs(LayoutChangeKind.WindowClosed));
    }

    // Lets the application edit the entries of a document tab's context menu before it opens.
    internal void RaiseDocumentTabContextMenuOpening(DocumentWindow document, IList<MenuFlyoutItemBase> items)
    {
        DocumentTabContextMenuOpening?.Invoke(this, new DocumentTabContextMenuEventArgs(document, items));
    }

    internal void NotifyLayoutChanged(LayoutChangeKind kind)
    {
        LayoutChanged?.Invoke(this, new LayoutChangedEventArgs(kind));
    }

    // Notes that a node of the layout has been moved, so that everything it carries is told about
    // it once the tree has settled.
    internal void NotifyRelocated(UIElement? node)
    {
        foreach (var relocatable in LayoutTree.Relocatables(node))
        {
            pendingRelocations.Add(relocatable);
        }

        if (relocationFlushScheduled || pendingRelocations.Count == 0)
        {
            return;
        }

        relocationFlushScheduled = true;
        LayoutUpdated += OnLayoutUpdatedAfterRelocation;
    }

    // Waits for the layout pass that puts the moved elements back in place before announcing the
    // move.
    //
    // WinUI raises the FrameworkElement.Loaded and FrameworkElement.Unloaded events of the moved
    // elements around this pass, and in that order, so a work item queued at low priority from here
    // is what makes Relocated the last word rather than one more event in the middle of the batch.
    private void OnLayoutUpdatedAfterRelocation(object? sender, object e)
    {
        LayoutUpdated -= OnLayoutUpdatedAfterRelocation;

        if (!DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FlushRelocations))
        {
            FlushRelocations();
        }
    }

    private void FlushRelocations()
    {
        relocationFlushScheduled = false;

        var relocated = pendingRelocations.ToList();
        pendingRelocations.Clear();

        foreach (var element in relocated)
        {
            // An element that is no longer in a tree was not relocated but removed, and its
            // Unloaded says exactly that.
            if (element is FrameworkElement { IsLoaded: false })
            {
                continue;
            }

            element.RaiseRelocated();
        }
    }

    private void OnAnyDescendantGotFocus(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var window = source as DockingWindow ?? source.FindAncestor<DockingWindow>();
        if (window is not null)
        {
            SetActiveWindow(window);
        }

        // The focus landing anywhere else is what ends an auto-hide panel's stay, whether it got
        // there by a click or by the keyboard. Inside the flyout it counts as staying — the panel's
        // own title bar and its content are part of it — and so does the edge it came from, whose
        // tabs are how the user reopens or swaps panels.
        if (autoHideFlyout?.Window is not null && !IsInsideAutoHideFlyout(source) && !IsInsideAutoHideStrip(source))
        {
            TryCollapseAutoHideFlyout(AutoHideDismissTrigger.FocusMovedOutside);
        }
    }
}
