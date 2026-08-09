using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

// Remembers where a container sat in the layout tree before it left it (auto-hidden to an edge or
// floated out), so pinning or docking it back restores the original position instead of falling
// back to a dock site edge.
internal sealed class DockRestoreHint
{
    internal DockRestoreHint(DockingWindowContainer? container, FrameworkElement? sibling, DockSide side, double relativeSize)
    {
        Container = container;
        Sibling = sibling;
        Side = side;
        RelativeSize = relativeSize;
    }

    // Gets the container the windows were taken from. When it is still part of the layout (because
    // other windows kept it alive) they rejoin it as tabs, which is where the user left them;
    // otherwise the position is rebuilt from Sibling.
    internal DockingWindowContainer? Container { get; }

    // Gets the neighbor pane the container sat next to, if any.
    internal FrameworkElement? Sibling { get; }

    // Gets the side of Sibling the container sat on.
    internal DockSide Side { get; }

    // Gets the container's relative size when it left the layout.
    internal double RelativeSize { get; }
}
