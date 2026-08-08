using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Digi21.WinUI.Docking;

/// <summary>
/// The root control of a docking layout. Hosts a tree of docking containers
/// (such as <c>SplitContainer</c> and <c>ToolWindowContainer</c>) and the central workspace.
/// </summary>
[ContentProperty(Name = nameof(Child))]
public partial class DockSite : Control
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

    private readonly List<ToolWindow> toolWindows = [];
    private readonly HashSet<FrameworkElement> dropTargets = [];
    private readonly List<AutoHideGroup> autoHideGroups = [];
    private readonly Dictionary<DockSide, AutoHideTabStrip> autoHideStrips = [];
    private AutoHideFlyout? autoHideFlyout;
    private ContentPresenter? layoutRootPresenter;

    /// <summary>Initializes a new instance of the <see cref="DockSite"/> class.</summary>
    public DockSite()
    {
        DefaultStyleKey = typeof(DockSite);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
        GotFocus += OnAnyDescendantGotFocus;
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
    /// Gets all tool windows known to this dock site, including closed ones that can be reopened.
    /// </summary>
    public IReadOnlyList<ToolWindow> ToolWindows => toolWindows;

    /// <summary>Gets the controller that runs interactive drag-and-drop re-docking, once the template is applied.</summary>
    internal DragDockController? DragController { get; private set; }

    /// <summary>Gets the presenter that hosts the docked layout, used as the coordinate reference for the edge strips.</summary>
    internal ContentPresenter? LayoutRoot => layoutRootPresenter;

    /// <summary>Gets the elements (containers and workspaces) that can receive dropped windows.</summary>
    internal IReadOnlyCollection<FrameworkElement> DropTargets => dropTargets;

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
            && GetTemplateChild("PART_DragGhost") is Border ghost
            && GetTemplateChild("PART_DragGhostText") is TextBlock ghostText
            && GetTemplateChild("PART_CenterGuides") is DockGuidePanel centerGuides
            && GetTemplateChild("PART_EdgeGuideLeft") is DockGuide left
            && GetTemplateChild("PART_EdgeGuideTop") is DockGuide top
            && GetTemplateChild("PART_EdgeGuideRight") is DockGuide right
            && GetTemplateChild("PART_EdgeGuideBottom") is DockGuide bottom)
        {
            DragController = new DragDockController(this, preview, ghost, ghostText, centerGuides, new Dictionary<DockSide, DockGuide>
            {
                [DockSide.Left] = left,
                [DockSide.Top] = top,
                [DockSide.Right] = right,
                [DockSide.Bottom] = bottom,
            });
        }
        else
        {
            DragController = null;
        }
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

    /// <summary>Adds an element to the set of drop targets considered during drag operations.</summary>
    internal void RegisterDropTarget(FrameworkElement element) => dropTargets.Add(element);

    /// <summary>Removes an element from the set of drop targets.</summary>
    internal void UnregisterDropTarget(FrameworkElement element) => dropTargets.Remove(element);

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
    internal void RegisterWindow(ToolWindow window)
    {
        if (!toolWindows.Contains(window))
        {
            toolWindows.Add(window);
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
