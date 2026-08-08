namespace Digi21.WinUI.Docking;

/// <summary>
/// Describes where a <see cref="DockingWindow"/> currently lives in the layout.
/// </summary>
public enum DockingWindowState
{
    /// <summary>The window is docked inside the <see cref="DockSite"/> layout tree.</summary>
    Docked,

    /// <summary>The window floats in its own top-level window. Not implemented yet.</summary>
    Floating,

    /// <summary>The window is unpinned to an auto-hide edge. Not implemented yet.</summary>
    AutoHide,

    /// <summary>The window is a tabbed document in the MDI area. Not implemented yet.</summary>
    Document,
}
