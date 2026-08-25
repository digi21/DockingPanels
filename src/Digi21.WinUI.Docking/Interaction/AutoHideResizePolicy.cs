namespace Digi21.WinUI.Docking.Interaction;

// How big an auto-hidden panel becomes while its grip is dragged. The rule lives here rather than
// in the grip so it can be read in one screen and tested without a XAML runtime: which way a drag
// makes the panel grow depends on the edge it came out of, and how far it may grow depends on the
// room the layout has left.
internal static class AutoHideResizePolicy
{
    // The floor a docked pane has. A panel dragged below it would be a title bar with no room for
    // anything under it, and the user would have no way back other than pinning it.
    internal const double MinimumSize = SplitContainer.MinPaneSize;

    // The panel slides over the workspace instead of taking it: whatever the drag asks for, a fifth
    // of the area stays visible. The same ceiling the flyout has always been positioned under.
    internal const double MaximumFraction = 0.8;

    // The size a panel takes when its grip has travelled the given distance along the axis it is
    // resized on. A panel at the left or top edge has its grip on the far side, so it grows as the
    // pointer moves away from the origin; one at the right or bottom grows as it moves towards it.
    internal static double SizeFromDrag(DockSide edge, double startSize, double delta)
    {
        return startSize + (edge is DockSide.Left or DockSide.Top ? delta : -delta);
    }

    // The size the panel is allowed to take within an area of the given extent. Also what keeps a
    // layout saved on a larger screen from covering a smaller one.
    internal static double Clamp(double size, double available)
    {
        if (!double.IsFinite(size))
        {
            return MinimumSize;
        }

        var maximum = double.IsFinite(available) ? Math.Max(MinimumSize, available * MaximumFraction) : MinimumSize;

        return Math.Clamp(size, MinimumSize, maximum);
    }
}
