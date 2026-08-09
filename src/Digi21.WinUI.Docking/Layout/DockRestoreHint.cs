using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Remembers where a container sat in the layout tree before it left it (auto-hidden to an edge
/// or floated out), so pinning or docking it back restores the original position instead of
/// falling back to a dock site edge.
/// </summary>
internal sealed class DockRestoreHint
{
    internal DockRestoreHint(ToolWindowContainer? container, FrameworkElement? sibling, DockSide side, double relativeSize)
    {
        Container = container;
        Sibling = sibling;
        Side = side;
        RelativeSize = relativeSize;
    }

    /// <summary>
    /// Gets the container the windows were taken from. When it is still part of the layout
    /// (because other windows kept it alive) they rejoin it as tabs, which is where the user
    /// left them; otherwise the position is rebuilt from <see cref="Sibling"/>.
    /// </summary>
    internal ToolWindowContainer? Container { get; }

    /// <summary>Gets the neighbor pane the container sat next to, if any.</summary>
    internal FrameworkElement? Sibling { get; }

    /// <summary>Gets the side of <see cref="Sibling"/> the container sat on.</summary>
    internal DockSide Side { get; }

    /// <summary>Gets the container's relative size when it left the layout.</summary>
    internal double RelativeSize { get; }
}
