namespace Digi21.WinUI.Docking;

// Keeps the tabs of a document group in two blocks: the pinned ones first, in their own order, and
// the rest after them.
//
// The blocks are an invariant of the group's Items collection rather than a way of drawing it, so
// the tab strip, the selected index, the insertion index a drag computes and the order the layout
// file records all agree on one list. Every rule below is expressed as indices over the pinned
// flags of a group, which is what makes it testable without a XAML runtime.
internal static class DocumentTabOrder
{
    // Returns the number of pinned tabs, which is where the pinned block ends.
    internal static int PinnedCount(IReadOnlyList<bool> pinned)
    {
        var count = 0;
        for (var i = 0; i < pinned.Count; i++)
        {
            if (pinned[i])
            {
                count++;
            }
        }

        return count;
    }

    // Returns where the tab at 'from' belongs once its pinned flag has changed, as an index into
    // the list the tab has already been taken out of.
    //
    // Both directions land on the same boundary: pinning moves the tab to the end of the pinned
    // block, unpinning moves it to the front of the normal block, and that is the same position —
    // the number of other pinned tabs. This is what Visual Studio does, and it means a tab pinned
    // and unpinned again does not travel back to where it started, which is deliberate: its old
    // neighbours may be gone by then.
    internal static int TargetIndexFor(IReadOnlyList<bool> pinned, int from)
    {
        var count = 0;
        for (var i = 0; i < pinned.Count; i++)
        {
            if (i != from && pinned[i])
            {
                count++;
            }
        }

        return count;
    }

    // Clamps the destination of a tab being moved inside its own group to the block it belongs to,
    // so dragging a pinned tab past the block does not silently unpin it, nor dragging a normal tab
    // to the far left pin it. Pinning stays an explicit gesture.
    //
    // Both 'from' and 'to' are indices into the group, 'to' being where the tab lands once it has
    // been taken out of it.
    internal static int ClampMove(IReadOnlyList<bool> pinned, int from, int to)
    {
        if (from < 0 || from >= pinned.Count)
        {
            return to;
        }

        var boundary = TargetIndexFor(pinned, from);
        var last = Math.Max(pinned.Count - 1, 0);

        return pinned[from]
            ? Math.Clamp(to, 0, boundary)
            : Math.Clamp(to, boundary, last);
    }

    // Clamps the position a tab is inserted at when it joins a group it is not in yet — a document
    // dropped from another group, or one being opened — to the block it belongs to. A negative
    // index means "append", which appends to the end of that block rather than of the strip.
    internal static int ClampInsertion(IReadOnlyList<bool> pinned, bool isPinned, int index)
    {
        var boundary = PinnedCount(pinned);

        if (isPinned)
        {
            return index < 0 ? boundary : Math.Clamp(index, 0, boundary);
        }

        return index < 0 ? pinned.Count : Math.Clamp(index, boundary, pinned.Count);
    }

    // Returns the order that puts the pinned tabs first while keeping the relative order inside
    // each block, as the old index of each tab in its new position, or null when the tabs are
    // already in two blocks.
    //
    // Used to settle a group whose order came from outside the rules above: a layout file that was
    // hand-edited, or an application inserting into the group's Items itself.
    internal static int[]? Partition(IReadOnlyList<bool> pinned)
    {
        var order = new int[pinned.Count];
        var next = 0;

        for (var i = 0; i < pinned.Count; i++)
        {
            if (pinned[i])
            {
                order[next++] = i;
            }
        }

        for (var i = 0; i < pinned.Count; i++)
        {
            if (!pinned[i])
            {
                order[next++] = i;
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
}
