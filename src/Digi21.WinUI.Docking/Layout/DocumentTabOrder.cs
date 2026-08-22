namespace Digi21.WinUI.Docking;

// The block a document's tab belongs to. The values are the order the blocks appear in, which is
// what every rule below is arithmetic over.
internal enum DocumentTabZone
{
    // Pinned to the head of the strip, in its own order, outside the part that scrolls.
    Pinned,

    // An ordinary tab.
    Normal,

    // The provisional (preview) tab: one per group, at the far end of the strip.
    Provisional,
}

// Keeps the tabs of a document group in blocks: the pinned ones first, the ordinary ones after
// them, and the provisional one last.
//
// The blocks are an invariant of the group's Items collection rather than a way of drawing it, so
// the tab strips, the selected index, the insertion index a drag computes and the order the layout
// file records all agree on one list. Every rule below is expressed as indices over the zones of a
// group, which is what makes it testable without a XAML runtime.
internal static class DocumentTabOrder
{
    // Returns the number of tabs before the given zone, which is where that zone begins.
    internal static int StartOf(IReadOnlyList<DocumentTabZone> zones, DocumentTabZone zone)
    {
        var count = 0;
        for (var i = 0; i < zones.Count; i++)
        {
            if (zones[i] < zone)
            {
                count++;
            }
        }

        return count;
    }

    // Returns the number of tabs up to and including the given zone, which is where that zone ends.
    internal static int EndOf(IReadOnlyList<DocumentTabZone> zones, DocumentTabZone zone)
    {
        var count = 0;
        for (var i = 0; i < zones.Count; i++)
        {
            if (zones[i] <= zone)
            {
                count++;
            }
        }

        return count;
    }

    // Returns where the tab at 'from' belongs once its zone has changed, as an index into the list
    // the tab has already been taken out of: the near edge of its new block, which is the one it
    // arrives from.
    //
    // Pinning a tab moves it to the *end* of the pinned block and unpinning it to the *front* of the
    // normal block; promoting the preview brings it to the end of the normal block, because it
    // arrives from the other side. Landing on the far edge instead would make a tab jump across
    // every tab of its new block for no reason the user asked for. Clamping the position it already
    // had into the new block says all of that at once.
    //
    // It also means a tab pinned and unpinned again does not travel back to where it started, which
    // is deliberate: its old neighbours may be gone by then.
    internal static int TargetIndexFor(IReadOnlyList<DocumentTabZone> zones, int from)
    {
        var zone = zones[from];
        var others = Without(zones, from);

        return Math.Clamp(from, StartOf(others, zone), EndOf(others, zone));
    }

    // Clamps the destination of a tab being moved inside its own group to the block it belongs to,
    // so dragging a pinned tab past the block does not silently unpin it, nor dragging a normal tab
    // to the far left pin it. Pinning stays an explicit gesture.
    //
    // A provisional tab is clamped to the *normal* block instead of to its own: dragging it is one
    // of the gestures that promotes it, so it is free to land anywhere among the ordinary tabs, and
    // the drop promotes it before the move is applied.
    //
    // Both 'from' and 'to' are indices into the group, 'to' being where the tab lands once it has
    // been taken out of it.
    internal static int ClampMove(IReadOnlyList<DocumentTabZone> zones, int from, int to)
    {
        if (from < 0 || from >= zones.Count)
        {
            return to;
        }

        var zone = zones[from] == DocumentTabZone.Provisional ? DocumentTabZone.Normal : zones[from];
        var others = Without(zones, from);

        return Math.Clamp(to, StartOf(others, zone), EndOf(others, zone));
    }

    // Clamps the position a tab is inserted at when it joins a group it is not in yet — a document
    // dropped from another group, or one being opened — to the block it belongs to. A negative
    // index means "append", which appends to the end of that block rather than of the strip: an
    // ordinary document lands before the provisional tab, never after it.
    internal static int ClampInsertion(IReadOnlyList<DocumentTabZone> zones, DocumentTabZone zone, int index)
    {
        var start = StartOf(zones, zone);
        var end = EndOf(zones, zone);

        return index < 0 ? end : Math.Clamp(index, start, end);
    }

    // Returns the order that puts the tabs in their blocks while keeping the relative order inside
    // each one, as the old index of each tab in its new position, or null when they are already in
    // blocks.
    //
    // Used to settle a group whose order came from outside the rules above: a layout file that was
    // hand-edited, or an application inserting into the group's Items itself.
    internal static int[]? Partition(IReadOnlyList<DocumentTabZone> zones)
    {
        var order = new int[zones.Count];
        var next = 0;

        foreach (var zone in new[] { DocumentTabZone.Pinned, DocumentTabZone.Normal, DocumentTabZone.Provisional })
        {
            for (var i = 0; i < zones.Count; i++)
            {
                if (zones[i] == zone)
                {
                    order[next++] = i;
                }
            }
        }

        for (var i = 0; i < order.Length; i++)
        {
            if (order[i] != i)
            {
                return order;
            }
        }

        return null;
    }

    // The zones of every tab but one, in order, which is the list an index computed after the tab
    // has been taken out refers to.
    private static List<DocumentTabZone> Without(IReadOnlyList<DocumentTabZone> zones, int index)
    {
        var rest = new List<DocumentTabZone>(Math.Max(zones.Count - 1, 0));

        for (var i = 0; i < zones.Count; i++)
        {
            if (i != index)
            {
                rest.Add(zones[i]);
            }
        }

        return rest;
    }
}
