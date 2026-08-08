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
}
