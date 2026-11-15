namespace Digi21.WinUI.Docking.Interaction;

// Decides whether an attention request on an auto-hidden panel also makes the panel peek out of its
// edge, or only lights its tab. Kept apart from the dock site for the same reason as the other
// auto-hide policies: it is the whole rule, and it can be tested without a XAML runtime.
internal static class AttentionPeekPolicy
{
    // Whether the site is set up to peek at all. Any of the three at zero turns it off, so an
    // application that sets the count to zero, or a theme that sets the distance to nothing, gets
    // the tab cue alone.
    internal static bool IsEnabled(int count, double distance, TimeSpan duration)
    {
        return count > 0 && double.IsFinite(distance) && distance > 0 && duration > TimeSpan.Zero;
    }

    // Whether this request is one to peek for, given what the panel has already done.
    //
    // A request that finds the tab unlit is news: the user has seen everything up to now, so the
    // panel peeks. A request that finds the tab still lit is more of the same, and content that is
    // written to many times a second asks on every write — without a limit the panel would never
    // stop moving. So a lit tab only peeks again once the minimum interval has passed since its last
    // peek. A panel that has never peeked has nothing to wait for.
    internal static bool IsDue(bool alreadyRequested, TimeSpan? sinceLastPeek, TimeSpan minimumInterval)
    {
        if (!alreadyRequested || sinceLastPeek is not { } elapsed)
        {
            return true;
        }

        return elapsed >= minimumInterval;
    }
}
