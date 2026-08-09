using System.Runtime.InteropServices;
using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Graphics;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Runs the interactive re-docking gesture for a <see cref="DockSite"/>: pointer-capture based
/// dragging of tool windows, dock guide display, drop target hit-testing, and the final layout
/// mutation on drop. The XAML drag-and-drop API is deliberately not used: it is a data transfer
/// API with system-drawn visuals and little mid-drag control.
/// </summary>
/// <remarks>
/// Dragging the caption of a floating window is a second, screen-coordinate based mode: the
/// gesture starts in another top-level window, so the dock site's guides have to be driven from
/// the cursor position instead of from local pointer arguments.
/// </remarks>
internal sealed partial class DragDockController
{
    private const double DragThreshold = 4.0;
    private const double EdgeGuideMargin = 16.0;
    private const double EdgeDockFraction = 0.25;
    private const int ExternalPollMilliseconds = 16;

    private readonly DockSite site;
    private readonly Rectangle preview;
    private readonly Border ghost;
    private readonly TextBlock ghostText;
    private readonly DockGuidePanel centerGuides;
    private readonly Dictionary<DockSide, DockGuide> edgeGuides;

    private ToolWindow? draggedWindow;
    private UIElement? source;
    private Pointer? pointer;
    private Point startPoint;
    private Point lastPoint;
    private bool dragActive;
    private FrameworkElement? hoveredTarget;
    private DockTarget currentTarget;

    private FloatingWindowHost? draggedHost;
    private bool movesHost;
    private DispatcherQueueTimer? externalTimer;
    private PointInt32 startCursor;
    private PointInt32 startHostPosition;
    private PointInt32 lastCursor;

    internal DragDockController(
        DockSite site,
        Rectangle preview,
        Border ghost,
        TextBlock ghostText,
        DockGuidePanel centerGuides,
        Dictionary<DockSide, DockGuide> edgeGuides)
    {
        this.site = site;
        this.preview = preview;
        this.ghost = ghost;
        this.ghostText = ghostText;
        this.centerGuides = centerGuides;
        this.edgeGuides = edgeGuides;
    }

    /// <summary>
    /// Starts tracking a pointer press on a tab or title bar. The drag only becomes visible
    /// once the pointer moves past a small threshold, so plain clicks are unaffected.
    /// </summary>
    internal void BeginPotentialDrag(ToolWindow window, UIElement sourceElement, PointerRoutedEventArgs e)
    {
        if (draggedWindow is not null || !window.CanDragWindow || !window.IsOpen)
        {
            return;
        }

        if (window.State == DockingWindowState.Floating)
        {
            if (site.FindFloatingHost(window) is { } host)
            {
                // Dragging the caption moves the whole floating window; dragging one of its
                // tabs pulls that single window out of it.
                BeginExternalDrag(window, sourceElement, e, host, sourceElement is ToolWindowTitleBar);
            }

            return;
        }

        if (!sourceElement.CapturePointer(e.Pointer))
        {
            return;
        }

        draggedWindow = window;
        source = sourceElement;
        pointer = e.Pointer;
        startPoint = e.GetCurrentPoint(site).Position;
        lastPoint = startPoint;
        dragActive = false;
        currentTarget = DockTarget.None;

        sourceElement.PointerMoved += OnPointerMoved;
        sourceElement.PointerReleased += OnPointerReleased;
        sourceElement.PointerCaptureLost += OnPointerCaptureLost;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (draggedWindow is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(site).Position;

        if (!dragActive)
        {
            if (Math.Abs(point.X - startPoint.X) < DragThreshold && Math.Abs(point.Y - startPoint.Y) < DragThreshold)
            {
                return;
            }

            StartDragVisuals(withGhost: true);
        }

        lastPoint = point;
        UpdateDrag(point);
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var window = draggedWindow;
        var target = currentTarget;
        var dropPoint = lastPoint;
        var wasActive = dragActive;

        EndDrag();

        if (wasActive && window is not null)
        {
            Perform(target, window, dropPoint);
        }

        e.Handled = wasActive;
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndDrag();
    }

    private void StartDragVisuals(bool withGhost)
    {
        dragActive = true;

        if (withGhost)
        {
            ghostText.Text = draggedWindow?.Title ?? string.Empty;
            ghost.Visibility = Visibility.Visible;
        }

        PositionEdgeGuides();
        foreach (var guide in edgeGuides.Values)
        {
            guide.Visibility = Visibility.Visible;
        }
    }

    private void UpdateDrag(Point point)
    {
        if (ghost.Visibility == Visibility.Visible)
        {
            Canvas.SetLeft(ghost, point.X + 16);
            Canvas.SetTop(ghost, point.Y + 16);
        }

        var target = HitTestTarget(point);
        if (!ReferenceEquals(target, hoveredTarget))
        {
            hoveredTarget = target;
            MoveClusterTo(target);
        }

        currentTarget = ResolveTarget(point, target);
        ApplyHotVisuals(currentTarget);
    }

    private void EndDrag()
    {
        if (source is not null)
        {
            source.PointerMoved -= OnPointerMoved;
            source.PointerReleased -= OnPointerReleased;
            source.PointerCaptureLost -= OnPointerCaptureLost;

            if (pointer is not null)
            {
                source.ReleasePointerCapture(pointer);
            }
        }

        ResetVisuals();

        draggedWindow = null;
        source = null;
        pointer = null;
        dragActive = false;
        hoveredTarget = null;
        currentTarget = DockTarget.None;
    }

    private void ResetVisuals()
    {
        ghost.Visibility = Visibility.Collapsed;
        preview.Visibility = Visibility.Collapsed;
        centerGuides.Visibility = Visibility.Collapsed;
        centerGuides.SetHotGuide(null, false);
        foreach (var guide in edgeGuides.Values)
        {
            guide.Visibility = Visibility.Collapsed;
            guide.IsHot = false;
        }
    }

    /// <summary>Applies the drop of a window dragged from inside the dock site.</summary>
    private void Perform(DockTarget target, ToolWindow window, Point dropPoint)
    {
        if (target.Kind != DockTargetKind.None)
        {
            LayoutManager.DockAtTarget(site, window, target);
            return;
        }

        if (FloatingHostUnderCursor() is { } host)
        {
            // Dropped on a floating window: join it as a tab.
            LayoutManager.AttachAsTab(site, window, host.Container);
            host.Activate();
            return;
        }

        // Released away from every guide: the window leaves the layout and floats, which
        // is how a floating window is created with the mouse.
        FloatAtPointer(window, dropPoint);
    }

    /// <summary>Finds the floating window the cursor is over, respecting z-order.</summary>
    private FloatingWindowHost? FloatingHostUnderCursor()
    {
        var hwnd = ScreenInterop.TopLevelWindowAt(ScreenInterop.CursorPosition);
        return hwnd == IntPtr.Zero
            ? null
            : site.FloatingHosts.FirstOrDefault(h => h.Handle == hwnd);
    }

    /// <summary>Floats a dragged window under the cursor, keeping the size it had while docked.</summary>
    private void FloatAtPointer(ToolWindow window, Point dropPoint)
    {
        if (!window.CanFloat)
        {
            return;
        }

        var (width, height) = FloatingWindowHost.PreferredSize(window.Container);
        var size = ScreenInterop.ToScreenSize(site, width, height);

        // Put the caption under the cursor, roughly where the user was holding the window.
        var grab = ScreenInterop.ToScreen(site, new Point(dropPoint.X - 24, dropPoint.Y - 12));
        if (grab is not { } origin)
        {
            site.FloatToolWindow(window);
            return;
        }

        site.FloatToolWindow(window, new RectInt32(origin.X, origin.Y, size.Width, size.Height));
    }

    /// <summary>
    /// Starts dragging a floating window's caption (or one of its tabs) over the dock site.
    /// The gesture lives in another top-level window, so it is tracked in screen coordinates:
    /// while the window follows the cursor its pointer never changes position inside the
    /// window, and XAML would stop reporting moves.
    /// </summary>
    private void BeginExternalDrag(
        ToolWindow window,
        UIElement sourceElement,
        PointerRoutedEventArgs e,
        FloatingWindowHost host,
        bool movesTheHost)
    {
        sourceElement.CapturePointer(e.Pointer);

        draggedWindow = window;
        draggedHost = host;
        movesHost = movesTheHost;
        source = sourceElement;
        pointer = e.Pointer;
        dragActive = false;
        currentTarget = DockTarget.None;
        startCursor = ScreenInterop.CursorPosition;
        lastCursor = startCursor;

        var bounds = host.Bounds;
        startHostPosition = new PointInt32(bounds.X, bounds.Y);

        sourceElement.PointerReleased += OnExternalPointerReleased;
        sourceElement.PointerCaptureLost += OnExternalPointerReleased;

        externalTimer = site.DispatcherQueue.CreateTimer();
        externalTimer.Interval = TimeSpan.FromMilliseconds(ExternalPollMilliseconds);
        externalTimer.IsRepeating = true;
        externalTimer.Tick += OnExternalTick;
        externalTimer.Start();
    }

    private void OnExternalTick(DispatcherQueueTimer sender, object args)
    {
        if (draggedHost is null)
        {
            EndExternalDrag(drop: false);
            return;
        }

        if (!IsPrimaryButtonDown())
        {
            // The button came up over another window, so no pointer event will arrive here.
            EndExternalDrag(drop: true);
            return;
        }

        var cursor = ScreenInterop.CursorPosition;
        if (cursor.X == lastCursor.X && cursor.Y == lastCursor.Y)
        {
            return;
        }

        lastCursor = cursor;
        var deltaX = cursor.X - startCursor.X;
        var deltaY = cursor.Y - startCursor.Y;

        if (!dragActive)
        {
            var threshold = DragThreshold * (site.XamlRoot?.RasterizationScale ?? 1.0);
            if (Math.Abs(deltaX) < threshold && Math.Abs(deltaY) < threshold)
            {
                return;
            }

            // The moved window is its own drag visual; a tab pulled out of it needs the ghost.
            StartDragVisuals(withGhost: !movesHost);
        }

        if (movesHost)
        {
            draggedHost.MoveTo(new PointInt32(startHostPosition.X + deltaX, startHostPosition.Y + deltaY));
        }

        if (ScreenInterop.FromScreen(site, cursor) is { } point && IsOverSite(point))
        {
            lastPoint = point;
            UpdateDrag(point);
        }
        else
        {
            ResetVisuals();
            StartDragVisuals(withGhost: false);
            hoveredTarget = null;
            currentTarget = DockTarget.None;
        }
    }

    private void OnExternalPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndExternalDrag(drop: true);
    }

    private void EndExternalDrag(bool drop)
    {
        var window = draggedWindow;
        var host = draggedHost;
        var target = currentTarget;
        var dropPoint = lastPoint;
        var wasActive = dragActive;
        var wasMovingHost = movesHost;

        if (externalTimer is not null)
        {
            externalTimer.Stop();
            externalTimer.Tick -= OnExternalTick;
            externalTimer = null;
        }

        if (source is not null)
        {
            source.PointerReleased -= OnExternalPointerReleased;
            source.PointerCaptureLost -= OnExternalPointerReleased;

            if (pointer is not null)
            {
                source.ReleasePointerCapture(pointer);
            }
        }

        ResetVisuals();

        draggedWindow = null;
        draggedHost = null;
        source = null;
        pointer = null;
        dragActive = false;
        movesHost = false;
        hoveredTarget = null;
        currentTarget = DockTarget.None;

        if (!drop || !wasActive || window is null || host is null)
        {
            return;
        }

        var dropHost = FloatingHostUnderCursor();

        // Docking closes the floating window this gesture started in; let its input event
        // finish first so the window is not torn down from inside its own handler.
        site.DispatcherQueue.TryEnqueue(() =>
        {
            if (wasMovingHost)
            {
                LayoutManager.DockFloatingHost(site, host, target);
            }
            else if (target.Kind != DockTargetKind.None)
            {
                LayoutManager.DockAtTarget(site, window, target);
            }
            else if (dropHost is not null && !ReferenceEquals(dropHost, host))
            {
                LayoutManager.AttachAsTab(site, window, dropHost.Container);
                dropHost.Activate();
            }
            else if (host.Container.Items.Count > 1 && dropHost is null)
            {
                // A tab dropped outside every window becomes a floating window of its own.
                FloatAtPointer(window, dropPoint);
            }
        });
    }

    private bool IsOverSite(Point point)
    {
        return point.X >= 0 && point.Y >= 0 && point.X <= site.ActualWidth && point.Y <= site.ActualHeight;
    }

    private void PositionEdgeGuides()
    {
        var width = site.ActualWidth;
        var height = site.ActualHeight;
        const double size = DockGuidePanel.GuideSize;

        Place(edgeGuides[DockSide.Left], EdgeGuideMargin, (height - size) / 2);
        Place(edgeGuides[DockSide.Right], width - size - EdgeGuideMargin, (height - size) / 2);
        Place(edgeGuides[DockSide.Top], (width - size) / 2, EdgeGuideMargin);
        Place(edgeGuides[DockSide.Bottom], (width - size) / 2, height - size - EdgeGuideMargin);

        static void Place(DockGuide guide, double x, double y)
        {
            Canvas.SetLeft(guide, x);
            Canvas.SetTop(guide, y);
        }
    }

    private void MoveClusterTo(FrameworkElement? target)
    {
        if (target is null)
        {
            centerGuides.Visibility = Visibility.Collapsed;
            return;
        }

        var bounds = BoundsIn(target);
        centerGuides.ShowCenter = target is ToolWindowContainer;
        Canvas.SetLeft(centerGuides, bounds.X + (bounds.Width - DockGuidePanel.ClusterSize) / 2);
        Canvas.SetTop(centerGuides, bounds.Y + (bounds.Height - DockGuidePanel.ClusterSize) / 2);
        centerGuides.Visibility = Visibility.Visible;
    }

    private FrameworkElement? HitTestTarget(Point point)
    {
        foreach (var target in site.DropTargets)
        {
            if (target.ActualWidth <= 0 || target.ActualHeight <= 0)
            {
                continue;
            }

            if (BoundsIn(target).Contains(point))
            {
                return target;
            }
        }

        return null;
    }

    private DockTarget ResolveTarget(Point point, FrameworkElement? target)
    {
        foreach (var (side, guide) in edgeGuides)
        {
            if (guide.Visibility == Visibility.Visible && ElementRect(guide, DockGuidePanel.GuideSize).Contains(point))
            {
                return new DockTarget(DockTargetKind.Edge, side, null);
            }
        }

        if (target is not null && centerGuides.Visibility == Visibility.Visible)
        {
            var clusterOrigin = new Point(Canvas.GetLeft(centerGuides), Canvas.GetTop(centerGuides));

            if (centerGuides.ShowCenter && ClusterGuideRect(clusterOrigin, null).Contains(point))
            {
                return new DockTarget(DockTargetKind.Tab, DockSide.Left, target);
            }

            foreach (var side in Enum.GetValues<DockSide>())
            {
                if (ClusterGuideRect(clusterOrigin, side).Contains(point))
                {
                    return new DockTarget(DockTargetKind.Relative, side, target);
                }
            }
        }

        return DockTarget.None;
    }

    private void ApplyHotVisuals(DockTarget target)
    {
        foreach (var (side, guide) in edgeGuides)
        {
            guide.IsHot = target.Kind == DockTargetKind.Edge && target.Side == side;
        }

        centerGuides.SetHotGuide(
            target.Kind == DockTargetKind.Relative ? target.Side : null,
            target.Kind == DockTargetKind.Tab);

        var previewRect = target switch
        {
            { Kind: DockTargetKind.Edge } => EdgePreviewRect(target.Side),
            { Kind: DockTargetKind.Relative, Element: { } element } => SidePreviewRect(BoundsIn(element), target.Side),
            { Kind: DockTargetKind.Tab, Element: { } element } => BoundsIn(element),
            _ => Rect.Empty,
        };

        if (previewRect.IsEmpty)
        {
            preview.Visibility = Visibility.Collapsed;
        }
        else
        {
            Canvas.SetLeft(preview, previewRect.X);
            Canvas.SetTop(preview, previewRect.Y);
            preview.Width = previewRect.Width;
            preview.Height = previewRect.Height;
            preview.Visibility = Visibility.Visible;
        }
    }

    private Rect EdgePreviewRect(DockSide side)
    {
        var width = site.ActualWidth;
        var height = site.ActualHeight;

        return side switch
        {
            DockSide.Left => new Rect(0, 0, width * EdgeDockFraction, height),
            DockSide.Right => new Rect(width * (1 - EdgeDockFraction), 0, width * EdgeDockFraction, height),
            DockSide.Top => new Rect(0, 0, width, height * EdgeDockFraction),
            _ => new Rect(0, height * (1 - EdgeDockFraction), width, height * EdgeDockFraction),
        };
    }

    private static Rect SidePreviewRect(Rect bounds, DockSide side)
    {
        return side switch
        {
            DockSide.Left => new Rect(bounds.X, bounds.Y, bounds.Width / 2, bounds.Height),
            DockSide.Right => new Rect(bounds.X + bounds.Width / 2, bounds.Y, bounds.Width / 2, bounds.Height),
            DockSide.Top => new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height / 2),
            _ => new Rect(bounds.X, bounds.Y + bounds.Height / 2, bounds.Width, bounds.Height / 2),
        };
    }

    private static Rect ClusterGuideRect(Point clusterOrigin, DockSide? side)
    {
        var offset = DockGuidePanel.GuideOffset(side);
        return new Rect(
            clusterOrigin.X + offset.X,
            clusterOrigin.Y + offset.Y,
            DockGuidePanel.GuideSize,
            DockGuidePanel.GuideSize);
    }

    private static Rect ElementRect(FrameworkElement element, double size)
    {
        return new Rect(Canvas.GetLeft(element), Canvas.GetTop(element), size, size);
    }

    private Rect BoundsIn(FrameworkElement element)
    {
        return element
            .TransformToVisual(site)
            .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
    }

    private static bool IsPrimaryButtonDown() => (GetAsyncKeyState(VirtualKeyLeftButton) & 0x8000) != 0;

    private const int VirtualKeyLeftButton = 0x01;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
