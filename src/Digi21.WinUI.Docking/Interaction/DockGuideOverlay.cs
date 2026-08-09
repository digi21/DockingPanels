using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Digi21.WinUI.Docking;

/// <summary>
/// The dock guides, drop preview and drag ghost of a single <see cref="IDockSurface"/>. It turns
/// a point over the surface into a <see cref="DockTarget"/> and shows what dropping there would do.
/// </summary>
/// <remarks>
/// Every surface owns one: the dock site draws its guides in its own window, and each floating
/// window draws them in its own, which is what a drag crossing windows needs, since neither
/// coordinates nor visuals can cross a XAML island.
/// </remarks>
internal sealed class DockGuideOverlay
{
    private const double EdgeGuideMargin = 16.0;
    private const double EdgeDockFraction = 0.25;

    private readonly IDockSurface surface;
    private readonly Rectangle preview;
    private readonly Border? ghost;
    private readonly TextBlock? ghostText;
    private readonly DockGuidePanel centerGuides;
    private readonly Dictionary<DockSide, DockGuide> edgeGuides;

    private FrameworkElement? hoveredTarget;

    internal DockGuideOverlay(
        IDockSurface surface,
        Rectangle preview,
        Border? ghost,
        TextBlock? ghostText,
        DockGuidePanel centerGuides,
        Dictionary<DockSide, DockGuide> edgeGuides)
    {
        this.surface = surface;
        this.preview = preview;
        this.ghost = ghost;
        this.ghostText = ghostText;
        this.centerGuides = centerGuides;
        this.edgeGuides = edgeGuides;
    }

    /// <summary>Shows the edge guides, and the drag ghost when the drag has no visual of its own.</summary>
    internal void Begin(bool withGhost, string title)
    {
        if (ghost is not null)
        {
            if (withGhost && ghostText is not null)
            {
                ghostText.Text = title;
            }

            ghost.Visibility = withGhost ? Visibility.Visible : Visibility.Collapsed;
        }

        PositionEdgeGuides();
        foreach (var guide in edgeGuides.Values)
        {
            guide.Visibility = Visibility.Visible;
        }
    }

    /// <summary>Updates the guides for a drag position and resolves what a drop there would do.</summary>
    /// <param name="point">The pointer position in the surface root's coordinates.</param>
    internal DockTarget Update(Point point)
    {
        if (ghost is { Visibility: Visibility.Visible })
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

        var resolved = ResolveTarget(point, target);
        ApplyHotVisuals(resolved);
        return resolved;
    }

    /// <summary>Hides every guide, the preview and the ghost.</summary>
    internal void Reset()
    {
        hoveredTarget = null;

        if (ghost is not null)
        {
            ghost.Visibility = Visibility.Collapsed;
        }

        preview.Visibility = Visibility.Collapsed;
        centerGuides.Visibility = Visibility.Collapsed;
        centerGuides.SetHotGuide(null, false);
        foreach (var guide in edgeGuides.Values)
        {
            guide.Visibility = Visibility.Collapsed;
            guide.IsHot = false;
        }
    }

    /// <summary>Tells whether a point in the surface root's coordinates is over the surface.</summary>
    internal bool Contains(Point point)
    {
        return point.X >= 0
            && point.Y >= 0
            && point.X <= surface.Root.ActualWidth
            && point.Y <= surface.Root.ActualHeight;
    }

    private void PositionEdgeGuides()
    {
        var width = surface.Root.ActualWidth;
        var height = surface.Root.ActualHeight;
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
        foreach (var target in surface.DropTargets)
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
        var width = surface.Root.ActualWidth;
        var height = surface.Root.ActualHeight;

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
            .TransformToVisual(surface.Root)
            .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
    }
}
