namespace Digi21.WinUI.Docking.Interaction;

// Decides whether something that could open an auto-hide flyout actually does. Kept apart from the
// controls that feed it for the same reason as AutoHideDismissPolicy: it is the whole rule, it fits
// on one line, and it can be tested without a XAML runtime — which matters more here than usual,
// since the gesture it governs is one no synthetic pointer input can reproduce.
internal static class AutoHideOpenPolicy
{
    internal static bool ShouldOpen(AutoHideOpenTrigger trigger, AutoHideOpenReason reason)
    {
        // Only the pointer is ever refused. Clicking a tab, and activating a window from code, are
        // requests for the panel however the site is configured — an application that calls
        // Activate, or a screen reader that selects the tab, is not hovering over anything.
        return reason != AutoHideOpenReason.Hover || trigger == AutoHideOpenTrigger.Pointer;
    }

    // How long the pointer has to rest on a tab before its panel slides out. Once a panel is out —
    // or still on its way back into the edge — pointing at another tab swaps it at once, as a menu
    // bar does: the user is already going through the panels.
    internal static TimeSpan HoverDelay(TimeSpan configured, bool panelShowing)
    {
        return panelShowing || configured <= TimeSpan.Zero ? TimeSpan.Zero : configured;
    }
}
