using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Runs the interactive re-docking gesture for a <see cref="DockSite"/>: pointer-capture based
/// dragging of tool windows, dock guide display, drop target hit-testing, and the final layout
/// mutation on drop. The XAML drag-and-drop API is deliberately not used: it is a data transfer
/// API with system-drawn visuals and little mid-drag control.
/// </summary>
internal sealed partial class DragDockController
{
    private const double DragThreshold = 4.0;
    private const double EdgeGuideMargin = 16.0;
    private const double EdgeDockFraction = 0.25;

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
    private bool dragActive;
    private FrameworkElement? hoveredTarget;
    private DropAction currentAction;

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

    private enum DropKind
    {
        None,
        Edge,
        Relative,
        Tab,
    }

    private readonly record struct DropAction(DropKind Kind, DockSide Side, FrameworkElement? Target);

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

        if (!sourceElement.CapturePointer(e.Pointer))
        {
            return;
        }

        draggedWindow = window;
        source = sourceElement;
        pointer = e.Pointer;
        startPoint = e.GetCurrentPoint(site).Position;
        dragActive = false;
        currentAction = new DropAction(DropKind.None, DockSide.Left, null);

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

            StartDragVisuals();
        }

        UpdateDrag(point);
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var window = draggedWindow;
        var action = currentAction;
        var wasActive = dragActive;

        EndDrag();

        if (wasActive && window is not null)
        {
            Perform(action, window);
        }

        e.Handled = wasActive;
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndDrag();
    }

    private void StartDragVisuals()
    {
        dragActive = true;
        ghostText.Text = draggedWindow?.Title ?? string.Empty;
        ghost.Visibility = Visibility.Visible;

        PositionEdgeGuides();
        foreach (var guide in edgeGuides.Values)
        {
            guide.Visibility = Visibility.Visible;
        }
    }

    private void UpdateDrag(Point point)
    {
        Canvas.SetLeft(ghost, point.X + 16);
        Canvas.SetTop(ghost, point.Y + 16);

        var target = HitTestTarget(point);
        if (!ReferenceEquals(target, hoveredTarget))
        {
            hoveredTarget = target;
            MoveClusterTo(target);
        }

        currentAction = ResolveAction(point, target);
        ApplyHotVisuals(currentAction);
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

        ghost.Visibility = Visibility.Collapsed;
        preview.Visibility = Visibility.Collapsed;
        centerGuides.Visibility = Visibility.Collapsed;
        centerGuides.SetHotGuide(null, false);
        foreach (var guide in edgeGuides.Values)
        {
            guide.Visibility = Visibility.Collapsed;
            guide.IsHot = false;
        }

        draggedWindow = null;
        source = null;
        pointer = null;
        dragActive = false;
        hoveredTarget = null;
        currentAction = new DropAction(DropKind.None, DockSide.Left, null);
    }

    private void Perform(DropAction action, ToolWindow window)
    {
        switch (action.Kind)
        {
            case DropKind.Edge:
                LayoutManager.DockToSide(site, window, action.Side);
                break;
            case DropKind.Relative when action.Target is not null:
                LayoutManager.DockRelativeTo(site, window, action.Target, action.Side);
                break;
            case DropKind.Tab when action.Target is ToolWindowContainer container:
                LayoutManager.AttachAsTab(site, window, container);
                break;
        }
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

    private DropAction ResolveAction(Point point, FrameworkElement? target)
    {
        foreach (var (side, guide) in edgeGuides)
        {
            if (guide.Visibility == Visibility.Visible && ElementRect(guide, DockGuidePanel.GuideSize).Contains(point))
            {
                return new DropAction(DropKind.Edge, side, null);
            }
        }

        if (target is not null && centerGuides.Visibility == Visibility.Visible)
        {
            var clusterOrigin = new Point(Canvas.GetLeft(centerGuides), Canvas.GetTop(centerGuides));

            if (centerGuides.ShowCenter && ClusterGuideRect(clusterOrigin, null).Contains(point))
            {
                return new DropAction(DropKind.Tab, DockSide.Left, target);
            }

            foreach (var side in Enum.GetValues<DockSide>())
            {
                if (ClusterGuideRect(clusterOrigin, side).Contains(point))
                {
                    return new DropAction(DropKind.Relative, side, target);
                }
            }
        }

        return new DropAction(DropKind.None, DockSide.Left, null);
    }

    private void ApplyHotVisuals(DropAction action)
    {
        foreach (var (side, guide) in edgeGuides)
        {
            guide.IsHot = action.Kind == DropKind.Edge && action.Side == side;
        }

        centerGuides.SetHotGuide(
            action.Kind == DropKind.Relative ? action.Side : null,
            action.Kind == DropKind.Tab);

        var previewRect = action.Kind switch
        {
            DropKind.Edge => EdgePreviewRect(action.Side),
            DropKind.Relative when action.Target is not null => SidePreviewRect(BoundsIn(action.Target), action.Side),
            DropKind.Tab when action.Target is not null => BoundsIn(action.Target),
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
}
