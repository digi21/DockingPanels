namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// Places the auto-hide tab groups of one edge along the strip.
/// </summary>
/// <remarks>
/// A group asks to sit at the offset its container had when it was unpinned, so its tabs stay
/// under where the panel used to be. That offset is only a wish: two groups routinely ask for the
/// same spot — a panel unpinned before the layout has been measured reports 0, and so does the one
/// that grew to take its place — and honouring both would draw one group's titles over the
/// other's. Groups are laid out in offset order, each pushed past the one before it.
/// </remarks>
internal static class AutoHideStripLayout
{
    /// <summary>The gap kept between two groups, so their tabs do not read as a single run.</summary>
    internal const double GroupGap = 8;

    /// <summary>
    /// Returns the offset along the strip for each group, in the order the groups were given.
    /// </summary>
    /// <param name="groups">The wished-for offset and the length along the strip of each group.</param>
    /// <param name="available">The length of the strip, or infinity when it is not known yet.</param>
    /// <param name="gap">The gap to keep between two groups.</param>
    internal static double[] Place(IReadOnlyList<(double Hint, double Length)> groups, double available, double gap = GroupGap)
    {
        var positions = new double[groups.Count];
        if (groups.Count == 0)
        {
            return positions;
        }

        // Ties keep the order the groups were added in, which is the order they were unpinned.
        var order = Enumerable.Range(0, groups.Count).OrderBy(i => Hint(groups[i].Hint)).ToArray();

        var cursor = 0.0;
        foreach (var i in order)
        {
            positions[i] = Math.Max(cursor, Hint(groups[i].Hint));
            cursor = positions[i] + Length(groups[i].Length) + gap;
        }

        var end = cursor - gap;
        if (!double.IsFinite(available) || end <= available)
        {
            return positions;
        }

        // The run overflows the edge: pull it back from the far end, keeping every group in order
        // and none of them on top of another.
        cursor = available;
        for (var k = order.Length - 1; k >= 0; k--)
        {
            var i = order[k];
            positions[i] = Math.Min(positions[i], cursor - Length(groups[i].Length));
            cursor = positions[i] - gap;
        }

        if (positions[order[0]] >= 0)
        {
            return positions;
        }

        // There are more tabs than edge to hang them on. Start at the corner and let the last ones
        // run off the end, which at least keeps the first ones readable.
        cursor = 0;
        foreach (var i in order)
        {
            positions[i] = cursor;
            cursor += Length(groups[i].Length) + gap;
        }

        return positions;
    }

    private static double Hint(double value) => double.IsFinite(value) && value > 0 ? value : 0;

    private static double Length(double value) => double.IsFinite(value) && value > 0 ? value : 0;
}
