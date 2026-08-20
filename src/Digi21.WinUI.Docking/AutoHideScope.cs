namespace Digi21.WinUI.Docking;

/// <summary>
/// How much of the layout <see cref="DockingWindow.AutoHide(AutoHideScope)"/> collapses to the edge.
/// </summary>
public enum AutoHideScope
{
    /// <summary>
    /// Collapse the window's whole container, so the windows sharing its tab group are
    /// auto-hidden with it and come back together. This is what unpinning from the title bar does.
    /// </summary>
    Container,

    /// <summary>
    /// Collapse only this window, leaving the rest of its container docked. The window comes back
    /// as a tab of that container, where the user left it, for as long as the container is still
    /// part of the layout.
    /// </summary>
    Window,
}
