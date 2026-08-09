using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Digi21.WinUI.Docking;

// The dock guides, drop preview and drag ghost of a single IDockSurface. It turns a point over the
// surface into a DockTarget and shows what dropping there would do.
//
// Every surface owns one: the dock site draws its guides in its own window, and each floating
// window draws them in its own, which is what a drag crossing windows needs, since neither
// coordinates nor visuals can cross a XAML island.
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

    // Where the guides ended up, in surface coordinates, measured once they were laid out. The
    // cluster's are kept as offsets from its origin so that they survive it being hidden while
    // the cursor slides along a tab strip.
    private readonly Dictionary<DockSide, Rect> edgeGuideRects = [];
    private readonly Dictionary<DockSide, Rect> clusterSideOffsets = [];
    private Rect clusterCenterOffset = Rect.Empty;

    private FrameworkElement? hoveredTarget;
    private bool documentDrag;

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

    // Shows the edge guides, and the drag ghost when the drag has no visual of its own.
    //
    // withGhost: Whether to show the drag ghost following the cursor.
    //
    // title: The title shown in the ghost.
    //
    // documents: Whether the drag carries documents rather than tool windows.
    internal void Begin(bool withGhost, string title, bool documents)
    {
        documentDrag = documents;

        if (ghost is not null)
        {
            if (withGhost && ghostText is not null)
            {
                ghostText.Text = title;
            }

            ghost.Visibility = withGhost ? Visibility.Visible : Visibility.Collapsed;
        }

        // Documents belong in the document area, so the edges of the dock site are no target for
        // them; inside a floating window there is no document area and the edges split the window.
        if (documentDrag && !surface.IsFloating)
        {
            return;
        }

        PositionEdgeGuides();
    }

    // Updates the guides for a drag position and resolves what a drop there would do.
    //
    // point: The pointer position in the surface root's coordinates.
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

    // Hides every guide, the preview and the ghost.
    internal void Reset()
    {
        hoveredTarget = null;
        edgeGuideRects.Clear();
        clusterSideOffsets.Clear();
        clusterCenterOffset = Rect.Empty;

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

    // Tells whether a point in the surface root's coordinates is over the surface.
    internal bool Contains(Point point)
    {
        return point.X >= 0
            && point.Y >= 0
            && point.X <= surface.Root.ActualWidth
            && point.Y <= surface.Root.ActualHeight;
    }

    // Centers each edge guide on its edge and records where it landed.
    //
    // The guides are shown and laid out before being placed, because their size comes from whatever
    // style is in effect, and a collapsed element has no size to center by.
    private void PositionEdgeGuides()
    {
        foreach (var guide in edgeGuides.Values)
        {
            guide.Visibility = Visibility.Visible;
        }

        surface.Root.UpdateLayout();

        var width = surface.Root.ActualWidth;
        var height = surface.Root.ActualHeight;

        edgeGuideRects.Clear();
        foreach (var (side, guide) in edgeGuides)
        {
            var size = guide.ActualWidth > 0 && guide.ActualHeight > 0
                ? new Size(guide.ActualWidth, guide.ActualHeight)
                : guide.DesiredSize;

            var (x, y) = side switch
            {
                DockSide.Left => (EdgeGuideMargin, (height - size.Height) / 2),
                DockSide.Right => (width - size.Width - EdgeGuideMargin, (height - size.Height) / 2),
                DockSide.Top => ((width - size.Width) / 2, EdgeGuideMargin),
                _ => ((width - size.Width) / 2, height - size.Height - EdgeGuideMargin),
            };

            Canvas.SetLeft(guide, x);
            Canvas.SetTop(guide, y);
            edgeGuideRects[side] = new Rect(x, y, size.Width, size.Height);
        }
    }

    private void MoveClusterTo(FrameworkElement? target)
    {
        clusterSideOffsets.Clear();
        clusterCenterOffset = Rect.Empty;

        if (target is null)
        {
            centerGuides.Visibility = Visibility.Collapsed;
            return;
        }

        // An empty document area is dropped into as a whole, so only its center guide is shown.
        centerGuides.ShowCenter = target is DockingWindowContainer or DocumentHost;
        centerGuides.ShowSides = target is not DocumentHost;
        centerGuides.Visibility = Visibility.Visible;

        // Laying the cluster out before centering it gives it the size its style asks for, and
        // gives each guide inside it the bounds the drop is later hit-tested against.
        centerGuides.UpdateLayout();

        var bounds = BoundsIn(target);
        Canvas.SetLeft(centerGuides, bounds.X + (bounds.Width - centerGuides.ActualWidth) / 2);
        Canvas.SetTop(centerGuides, bounds.Y + (bounds.Height - centerGuides.ActualHeight) / 2);

        clusterCenterOffset = centerGuides.GuideBounds(null);
        foreach (var side in Enum.GetValues<DockSide>())
        {
            clusterSideOffsets[side] = centerGuides.GuideBounds(side);
        }
    }

    private FrameworkElement? HitTestTarget(Point point)
    {
        foreach (var target in Targets())
        {
            if (target.ActualWidth <= 0 || target.ActualHeight <= 0 || !Accepts(target))
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

    // Enumerates what a drop can land on, from the surface's current layout tree.
    private IEnumerable<FrameworkElement> Targets() => LayoutTree.DropTargets(surface.LayoutChild);

    // Tells whether a drop target takes what is being dragged: documents only go into the document
    // area, and tool windows only into the panes around it.
    private bool Accepts(FrameworkElement target)
    {
        return documentDrag
            ? target is DocumentContainer or DocumentHost
            : target is ToolWindowContainer or Workspace;
    }

    private DockTarget ResolveTarget(Point point, FrameworkElement? target)
    {
        foreach (var (side, guide) in edgeGuides)
        {
            if (guide.Visibility == Visibility.Visible
                && edgeGuideRects.TryGetValue(side, out var rect)
                && rect.Contains(point))
            {
                return new DockTarget(DockTargetKind.Edge, side, null);
            }
        }

        // Over the tab strip of a pane the drop lands at a tab position, which is what turns
        // dragging a tab along its own strip into a reorder instead of a re-dock.
        if (target is DockingWindowContainer container)
        {
            var local = ToLocal(container, point);
            var strip = container.TabStripBounds;
            if (!strip.IsEmpty && strip.Contains(local))
            {
                return new DockTarget(
                    DockTargetKind.Tab,
                    DockSide.Left,
                    container,
                    container.InsertionIndexAt(local));
            }
        }

        // The cluster is hidden while the cursor slides along a tab strip, but it stays where it
        // was placed, so what decides whether its guides can be hit is the hovered target.
        if (target is not null && ReferenceEquals(target, hoveredTarget))
        {
            var clusterOrigin = new Point(Canvas.GetLeft(centerGuides), Canvas.GetTop(centerGuides));

            if (centerGuides.ShowCenter && ClusterGuideRect(clusterOrigin, clusterCenterOffset).Contains(point))
            {
                return new DockTarget(DockTargetKind.Tab, DockSide.Left, target);
            }

            if (centerGuides.ShowSides)
            {
                foreach (var (side, offset) in clusterSideOffsets)
                {
                    if (ClusterGuideRect(clusterOrigin, offset).Contains(point))
                    {
                        return new DockTarget(DockTargetKind.Relative, side, target);
                    }
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
            target is { Kind: DockTargetKind.Tab, Index: < 0 });

        if (hoveredTarget is not null)
        {
            // Sliding a tab along a strip is a reorder: the caret says where it lands, and the
            // guides would only be in the way.
            centerGuides.Visibility = target is { Kind: DockTargetKind.Tab, Index: >= 0 }
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        var previewRect = target switch
        {
            { Kind: DockTargetKind.Edge } => EdgePreviewRect(target.Side),
            { Kind: DockTargetKind.Relative, Element: { } element } => SidePreviewRect(BoundsIn(element), target.Side),
            { Kind: DockTargetKind.Tab, Element: DockingWindowContainer pane, Index: >= 0 } =>
                ToSurface(pane, pane.InsertionMarker(target.Index)),
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

    // Turns a guide's offset within the cluster into a rectangle in surface coordinates.
    private static Rect ClusterGuideRect(Point clusterOrigin, Rect offset)
    {
        return offset.IsEmpty
            ? Rect.Empty
            : new Rect(clusterOrigin.X + offset.X, clusterOrigin.Y + offset.Y, offset.Width, offset.Height);
    }

    // Converts a point in the surface's coordinates into an element's own coordinates.
    private Point ToLocal(FrameworkElement element, Point point)
    {
        var bounds = BoundsIn(element);
        return new Point(point.X - bounds.X, point.Y - bounds.Y);
    }

    // Converts a rectangle in an element's coordinates into the surface's coordinates.
    private Rect ToSurface(FrameworkElement element, Rect rect)
    {
        if (rect.IsEmpty)
        {
            return Rect.Empty;
        }

        var bounds = BoundsIn(element);
        return new Rect(bounds.X + rect.X, bounds.Y + rect.Y, rect.Width, rect.Height);
    }

    private Rect BoundsIn(FrameworkElement element)
    {
        return element
            .TransformToVisual(surface.Root)
            .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
    }
}
