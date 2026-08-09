using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

    private readonly List<ToolWindow> toolWindows = [];
    private readonly List<DocumentWindow> documents = [];
    private readonly List<AutoHideGroup> autoHideGroups = [];
    private readonly List<FloatingWindowHost> floatingHosts = [];
    private readonly Dictionary<DockSide, AutoHideTabStrip> autoHideStrips = [];
    private AutoHideFlyout? autoHideFlyout;
    private ContentPresenter? layoutRootPresenter;
    private AppWindow? ownerWindow;
    private DockGuideOverlay? overlay;

    /// <summary>Initializes a new instance of the <see cref="DockSite"/> class.</summary>
    public DockSite()
    {
        DefaultStyleKey = typeof(DockSite);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
        GotFocus += OnAnyDescendantGotFocus;
        Loaded += (_, _) => HookOwnerWindow();
        Unloaded += (_, _) => CloseFloatingWindows();
    }

    /// <summary>
    /// Closes the floating windows while the window hosting this dock site is still alive.
    /// They are owned windows, so leaving them to be destroyed together with their owner tears
    /// down their XAML islands during the owner's own teardown, which crashes the process.
    /// </summary>
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

    private void OnOwnerWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
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
    /// Gets or sets the root element of the docking layout tree.
    /// </summary>
    public UIElement? Child
    {
        get => (UIElement?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
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
    public IReadOnlyList<ToolWindow> ToolWindows => toolWindows;

    /// <summary>
    /// Gets all documents known to this dock site, including closed ones that can be reopened.
    /// </summary>
    public IReadOnlyList<DocumentWindow> Documents => documents;

    /// <summary>
    /// Gets the document area of the layout, or <see langword="null"/> when the layout has none.
    /// </summary>
    public DocumentHost? DocumentHost => LayoutTree.DocumentHosts(Child).FirstOrDefault();

    /// <summary>Gets the controller that runs interactive drag-and-drop re-docking, once the template is applied.</summary>
    internal DragDockController? DragController { get; private set; }

    /// <summary>Gets the presenter that hosts the docked layout, used as the coordinate reference for the edge strips.</summary>
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
        layoutRootPresenter = GetTemplateChild("PART_LayoutRoot") as ContentPresenter;
        RefreshAutoHideStrips();
        AddHandler(PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(OnDismissPointerPressed), true);

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
    /// <param name="target">An open window whose container is the dock target.</param>
    /// <param name="side">The side of the target to dock to.</param>
    public void DockToolWindow(ToolWindow window, DockingWindow target, DockSide side)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(target);

        if (target.Container is not { } container)
        {
            throw new InvalidOperationException("The target window is not open, so there is no container to dock beside.");
        }

        LayoutManager.DockRelativeTo(this, window, container, side);
    }

    /// <summary>
    /// Attaches a tool window as a new tab in the container of another window.
    /// </summary>
    /// <param name="window">The window to attach.</param>
    /// <param name="target">An open window whose container receives the new tab.</param>
    public void AttachToolWindow(ToolWindow window, DockingWindow target)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(target);

        if (target.Container is not { } container)
        {
            throw new InvalidOperationException("The target window is not open, so there is no container to attach to.");
        }

        LayoutManager.AttachAsTab(this, window, container);
    }

    /// <summary>
    /// Opens a document in the document area, as a new tab of its active group. Also reopens
    /// closed documents.
    /// </summary>
    /// <param name="document">The document to open.</param>
    /// <exception cref="InvalidOperationException">The layout has no <see cref="DocumentHost"/>.</exception>
    public void OpenDocument(DocumentWindow document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (DocumentHost is not { } host)
        {
            throw new InvalidOperationException("The layout of this dock site has no DocumentHost to open documents in.");
        }

        host.OpenDocument(document);
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

    /// <summary>Gets the floating windows opened by this dock site.</summary>
    internal IReadOnlyList<FloatingWindowHost> FloatingHosts => floatingHosts;

    internal void AddFloatingHost(FloatingWindowHost host) => floatingHosts.Add(host);

    internal void RemoveFloatingHost(FloatingWindowHost host) => floatingHosts.Remove(host);

    /// <summary>Finds the floating window hosting the given window, if any.</summary>
    internal FloatingWindowHost? FindFloatingHost(DockingWindow window)
    {
        return floatingHosts.FirstOrDefault(h => h.Windows.Contains(window));
    }

    /// <summary>Finds the floating window with the given window handle, if it belongs to this site.</summary>
    internal FloatingWindowHost? FindFloatingHost(IntPtr handle)
    {
        return handle == IntPtr.Zero ? null : floatingHosts.FirstOrDefault(h => h.Handle == handle);
    }

    /// <summary>Closes every floating window, releasing the tool windows they host.</summary>
    internal void CloseFloatingWindows()
    {
        foreach (var host in floatingHosts.ToList())
        {
            host.ReleaseAndClose();
        }
    }

    /// <summary>Picks the initial bounds of a floating window: the size it has while docked, cascaded from the site's corner.</summary>
    private RectInt32 DefaultFloatingBounds(DockingWindow window)
    {
        var (width, height) = FloatingWindowHost.PreferredSize(window.Container);
        var size = ScreenInterop.ToScreenSize(this, width, height);
        var cascade = 24.0 * (floatingHosts.Count + 1);
        var origin = ScreenInterop.ToScreen(this, new Windows.Foundation.Point(cascade, cascade))
            ?? new Windows.Graphics.PointInt32(120, 120);

        return new RectInt32(origin.X, origin.Y, size.Width, size.Height);
    }

    /// <summary>Gets the auto-hide groups collapsed to the edges of this dock site.</summary>
    internal IReadOnlyList<AutoHideGroup> AutoHideGroups => autoHideGroups;

    /// <summary>Finds the auto-hide group containing the given window, if any.</summary>
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

    /// <summary>Synchronizes the edge tab strips with the current auto-hide groups.</summary>
    internal void RefreshAutoHideStrips()
    {
        foreach (var (side, strip) in autoHideStrips)
        {
            var groups = autoHideGroups.Where(g => g.Edge == side && g.Windows.Count > 0).ToList();
            strip.SetGroups(groups);
            strip.Visibility = groups.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>Shows the auto-hide flyout for the given window.</summary>
    internal void ShowAutoHideFlyout(ToolWindow window)
    {
        if (autoHideFlyout is null || layoutRootPresenter is null
            || FindAutoHideGroup(window) is not { } group)
        {
            return;
        }

        var cellWidth = layoutRootPresenter.ActualWidth;
        var cellHeight = layoutRootPresenter.ActualHeight;
        var size = Math.Min(group.Size, (group.Edge is DockSide.Left or DockSide.Right ? cellWidth : cellHeight) * 0.8);

        var (width, height) = group.Edge is DockSide.Left or DockSide.Right
            ? (size, cellHeight)
            : (cellWidth, size);

        autoHideFlyout.Show(window, width, height);

        var origin = layoutRootPresenter
            .TransformToVisual(this)
            .TransformPoint(new Windows.Foundation.Point(0, 0));
        Canvas.SetLeft(autoHideFlyout, origin.X + (group.Edge == DockSide.Right ? cellWidth - width : 0));
        Canvas.SetTop(autoHideFlyout, origin.Y + (group.Edge == DockSide.Bottom ? cellHeight - height : 0));
        autoHideFlyout.Visibility = Visibility.Visible;

        SetActiveWindow(window);
    }

    /// <summary>Hides the auto-hide flyout if it is open.</summary>
    internal void HideAutoHideFlyout()
    {
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

    private void OnDismissPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (autoHideFlyout?.Window is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this).Position;
        if (IsWithin(autoHideFlyout, point))
        {
            return;
        }

        foreach (var strip in autoHideStrips.Values)
        {
            if (strip.Visibility == Visibility.Visible && IsWithin(strip, point))
            {
                return;
            }
        }

        HideAutoHideFlyout();

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

    /// <summary>Adds a window to the registry of known windows.</summary>
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

    /// <summary>Makes the given window the active one, deactivating the previous active window.</summary>
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

    /// <summary>Raises <see cref="WindowClosing"/> and returns whether the close may proceed.</summary>
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

    internal void NotifyLayoutChanged(LayoutChangeKind kind)
    {
        LayoutChanged?.Invoke(this, new LayoutChangedEventArgs(kind));
    }

    private void OnAnyDescendantGotFocus(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            var window = source as DockingWindow ?? source.FindAncestor<DockingWindow>();
            if (window is not null)
            {
                SetActiveWindow(window);
            }
        }
    }
}
