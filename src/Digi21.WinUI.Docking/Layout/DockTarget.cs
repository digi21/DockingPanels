using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

/// <summary>Describes what a drop should do with the dragged windows.</summary>
internal enum DockTargetKind
{
    /// <summary>Nothing was hit: the drop leaves the windows where they are, or floats them.</summary>
    None,

    /// <summary>Dock back where the windows came from, using their restore hint.</summary>
    Home,

    /// <summary>Dock as a new pane at an edge of the whole dock site.</summary>
    Edge,

    /// <summary>Dock as a new pane beside an existing layout node.</summary>
    Relative,

    /// <summary>Attach as tabs of an existing container.</summary>
    Tab,
}

/// <summary>
/// A resolved drop position: what kind of docking to perform, on which side, and against which
/// element. Shared by the drag controller and the layout manager so an interactive drop and a
/// programmatic one take exactly the same path.
/// </summary>
internal readonly record struct DockTarget(DockTargetKind Kind, DockSide Side, FrameworkElement? Element)
{
    /// <summary>Gets a target that docks the windows back where they came from.</summary>
    internal static DockTarget Home { get; } = new(DockTargetKind.Home, DockSide.Left, null);

    /// <summary>Gets a target that means "no drop position was hit".</summary>
    internal static DockTarget None { get; } = new(DockTargetKind.None, DockSide.Left, null);
}
