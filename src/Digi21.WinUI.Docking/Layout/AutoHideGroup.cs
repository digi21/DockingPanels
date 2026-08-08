using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

/// <summary>
/// A group of tool windows that were auto-hidden together (they shared a container when
/// unpinned). The group keeps the edge it collapsed to and the flyout size, and is re-docked
/// as a whole when any of its windows is pinned back.
/// </summary>
internal sealed class AutoHideGroup
{
    internal AutoHideGroup(DockSide edge, List<ToolWindow> windows, double size)
    {
        Edge = edge;
        Windows = windows;
        Size = size;
    }

    /// <summary>Gets the dock site edge the group is collapsed to.</summary>
    internal DockSide Edge { get; }

    /// <summary>Gets the windows in the group, in tab order.</summary>
    internal List<ToolWindow> Windows { get; }

    /// <summary>Gets or sets the flyout size in pixels (width for vertical edges, height for horizontal ones).</summary>
    internal double Size { get; set; }

    /// <summary>
    /// Gets or sets the neighbor pane the group's container sat next to when it was unpinned,
    /// so pinning it back can restore the original position instead of docking to the site edge.
    /// </summary>
    internal FrameworkElement? RestoreSibling { get; set; }

    /// <summary>Gets or sets the side of <see cref="RestoreSibling"/> the container sat on.</summary>
    internal DockSide RestoreSide { get; set; }

    /// <summary>Gets or sets the container's relative size at unpin time.</summary>
    internal double RestoreRelativeSize { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the position of the container along the edge at unpin time (X for
    /// top/bottom edges, Y for left/right ones), so the tab group appears aligned with
    /// where the container was instead of at the start of the strip.
    /// </summary>
    internal double Offset { get; set; }
}
