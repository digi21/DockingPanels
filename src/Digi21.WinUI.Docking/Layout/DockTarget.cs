using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

// Describes what a drop should do with the dragged windows.
internal enum DockTargetKind
{
    // Nothing was hit: the drop leaves the windows where they are, or floats them.
    None,

    // Dock back where the windows came from, using their restore hint.
    Home,

    // Dock as a new pane at an edge of the whole dock site.
    Edge,

    // Dock as a new pane beside an existing layout node.
    Relative,

    // Attach as tabs of an existing container.
    Tab,
}

// A resolved drop position: what kind of docking to perform, on which side, and against which
// element. Shared by the drag controller and the layout manager so an interactive drop and a
// programmatic one take exactly the same path.
//
// Kind: What the drop does with the dragged windows.
//
// Side: The side to dock to, for edge and relative drops.
//
// Element: The element the drop acts on, for relative and tab drops.
//
// Index: The tab position a tab drop lands on, or -1 to append. This is what turns dragging a tab
// along its own strip into a reorder instead of a re-dock.
internal readonly record struct DockTarget(DockTargetKind Kind, DockSide Side, FrameworkElement? Element, int Index = -1)
{
    // Gets a target that docks the windows back where they came from.
    internal static DockTarget Home { get; } = new(DockTargetKind.Home, DockSide.Left, null);

    // Gets a target that means "no drop position was hit".
    internal static DockTarget None { get; } = new(DockTargetKind.None, DockSide.Left, null);
}
