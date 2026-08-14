namespace Digi21.WinUI.Docking.Interaction;

// Why the auto-hide flyout is open. Pointing at a tab opens a preview, which gets out of the way
// as soon as the pointer leaves; opening it deliberately, or typing into it, makes it a panel the
// user is working in, and those only close when the work moves elsewhere.
internal enum AutoHideOpenReason
{
    // The pointer came to rest on the window's tab.
    Hover,

    // The tab was clicked, the window was activated from code, or the user clicked inside the
    // flyout after pointing at it.
    Activation,
}

// What happened that could collapse the flyout.
internal enum AutoHideDismissTrigger
{
    // The pointer left both the flyout and the tab strips.
    PointerLeft,

    // A pointer press landed somewhere outside the flyout and the strips.
    PointerPressedOutside,

    // The focus moved to an element outside the flyout.
    FocusMovedOutside,
}

// Decides whether an open auto-hide flyout collapses back to its edge. This is the whole rule, kept
// away from the controls that feed it so it can be read in one screen and tested without a XAML
// runtime: what matters is how the flyout was opened and whether anything inside it holds the
// focus, never how long it has been open.
internal static class AutoHideDismissPolicy
{
    internal static bool ShouldCollapse(AutoHideOpenReason reason, AutoHideDismissTrigger trigger, bool focusInside)
    {
        return trigger switch
        {
            // The focus is now on something else. Whatever opened the flyout, the user has moved on.
            AutoHideDismissTrigger.FocusMovedOutside => true,

            // A preview follows the pointer. Once a control inside holds the focus the flyout is no
            // longer a preview, and the pointer wandering off is not how the user dismisses it —
            // which is the case that used to close the panel being typed into.
            AutoHideDismissTrigger.PointerLeft => reason == AutoHideOpenReason.Hover && !focusInside,

            // A click elsewhere normally takes the focus with it, and then the first rule applies.
            // When it does not — empty chrome, a splitter, a control that refuses focus — a flyout
            // holding the focus stays, and one that never had it collapses as before.
            AutoHideDismissTrigger.PointerPressedOutside => !focusInside,

            _ => false,
        };
    }
}
