namespace Digi21.WinUI.Docking;

// A group of tool windows that were auto-hidden together (they shared a container when unpinned).
// The group keeps the edge it collapsed to and the flyout size, and is re-docked as a whole when
// any of its windows is pinned back.
internal sealed class AutoHideGroup
{
    internal AutoHideGroup(DockSide edge, List<ToolWindow> windows, double size)
    {
        Edge = edge;
        Windows = windows;
        Size = size;
    }

    // Gets the dock site edge the group is collapsed to.
    internal DockSide Edge { get; }

    // Gets the windows in the group, in tab order.
    internal List<ToolWindow> Windows { get; }

    // Gets or sets the flyout size in pixels (width for vertical edges, height for horizontal
    // ones).
    internal double Size { get; set; }

    // Gets or sets where the group re-docks when it is pinned back into the layout.
    internal DockRestoreHint? RestoreHint { get; set; }

    // Gets or sets the position of the container along the edge at unpin time (X for top/bottom
    // edges, Y for left/right ones), so the tab group appears aligned with where the container was
    // instead of at the start of the strip.
    internal double Offset { get; set; }
}
