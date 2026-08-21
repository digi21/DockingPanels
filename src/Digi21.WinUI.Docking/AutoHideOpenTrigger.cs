namespace Digi21.WinUI.Docking;

/// <summary>
/// What it takes for an auto-hidden panel to slide out when the user goes for its tab.
/// </summary>
public enum AutoHideOpenTrigger
{
    /// <summary>
    /// Pointing at a tab is enough: the panel slides out as a preview, which is not activated and
    /// slides back when the pointer leaves. Clicking a tab still opens the panel for real. This is
    /// the default.
    /// </summary>
    Pointer,

    /// <summary>
    /// Only a click opens the panel. Pointing at a tab does nothing, so a pointer crossing the edge
    /// on its way somewhere else leaves the layout alone.
    /// </summary>
    /// <remarks>
    /// A panel opened this way was asked for, so it stays until the focus moves elsewhere or the
    /// user clicks outside it — the pointer wandering off does not dismiss it, and
    /// <see cref="DockSite.AutoHideCloseDelay"/> has nothing to cushion.
    /// </remarks>
    Click,
}
