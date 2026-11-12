using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking;

// What DocumentHost.Arrange decides before it touches a single control, kept apart from the controls so that it can be
// tested without a XAML runtime: which documents end up in which group, which groups and splits survive, and how the
// space is shared. The nodes are generic over the document type; the host uses DocumentWindow, the tests use strings.
internal abstract record ArrangementNode<T>(double Size)
    where T : class;

// A tab group: its documents in tab order and the one shown (null: the first).
internal sealed record ArrangementGroup<T>(double Size, IReadOnlyList<T> Items, T? Selected) : ArrangementNode<T>(Size)
    where T : class;

// A split of panes, side by side (Horizontal) or stacked (Vertical).
internal sealed record ArrangementSplit<T>(double Size, Orientation Orientation, IReadOnlyList<ArrangementNode<T>> Children)
    : ArrangementNode<T>(Size)
    where T : class;

internal static class DocumentArrangementPlanner
{
    // Turns a requested arrangement into the one to build.
    //
    // requested: what the caller described.
    //
    // unlisted: the documents the area holds that the description does not name. They are not closed, and not left
    // outside: they become tabs of the last group, after its own.
    //
    // Returns the arrangement to build, or null when no document is left to arrange. A group that holds nothing is
    // dropped, a split with one pane left is replaced by that pane (which keeps the split's share), and the shares of
    // the siblings that remain are scaled to add up to 1.
    //
    // A document named twice is a mistake the caller can see, so it throws instead of picking one of them.
    internal static ArrangementNode<T>? Plan<T>(ArrangementNode<T> requested, IEnumerable<T> unlisted)
        where T : class
    {
        var listed = new HashSet<T>(ReferenceEqualityComparer.Instance);
        foreach (var item in Items(requested))
        {
            if (!listed.Add(item))
            {
                throw new ArgumentException($"The document '{item}' is in the layout twice; a document can be in one group only.", nameof(requested));
            }
        }

        var extra = unlisted.Where(item => !listed.Contains(item)).Distinct<T>(ReferenceEqualityComparer.Instance).ToList();
        var lastGroup = LastGroup(requested);
        var withExtras = extra.Count == 0 || lastGroup is null ? requested : Rebuild(requested, lastGroup, extra);
        if (extra.Count > 0 && lastGroup is null)
        {
            // A description without a single group: the documents it did not name get one of their own.
            return Normalise(new ArrangementGroup<T>(1.0, extra, null));
        }

        return Normalise(withExtras);
    }

    private static IEnumerable<T> Items<T>(ArrangementNode<T> node)
        where T : class
    {
        switch (node)
        {
            case ArrangementGroup<T> group:
                foreach (var item in group.Items)
                {
                    yield return item;
                }

                break;
            case ArrangementSplit<T> split:
                foreach (var child in split.Children)
                {
                    foreach (var item in Items(child))
                    {
                        yield return item;
                    }
                }

                break;
        }
    }

    private static ArrangementGroup<T>? LastGroup<T>(ArrangementNode<T> node)
        where T : class
    {
        switch (node)
        {
            case ArrangementGroup<T> group:
                return group;
            case ArrangementSplit<T> split:
                for (var i = split.Children.Count - 1; i >= 0; i--)
                {
                    if (LastGroup(split.Children[i]) is { } found)
                    {
                        return found;
                    }
                }

                return null;
            default:
                return null;
        }
    }

    // The tree with the extra documents added to one group.
    private static ArrangementNode<T> Rebuild<T>(ArrangementNode<T> node, ArrangementGroup<T> target, List<T> extra)
        where T : class
    {
        return node switch
        {
            ArrangementGroup<T> group when ReferenceEquals(group, target) => group with { Items = [.. group.Items, .. extra] },
            ArrangementSplit<T> split => split with { Children = [.. split.Children.Select(child => Rebuild(child, target, extra))] },
            _ => node,
        };
    }

    // Drops what is empty, collapses what holds one pane, and shares the space out again.
    private static ArrangementNode<T>? Normalise<T>(ArrangementNode<T> node)
        where T : class
    {
        switch (node)
        {
            case ArrangementGroup<T> group:
                return group.Items.Count == 0 ? null : group;

            case ArrangementSplit<T> split:
                var children = split.Children.Select(Normalise).OfType<ArrangementNode<T>>().ToList();
                if (children.Count == 0)
                {
                    return null;
                }

                if (children.Count == 1)
                {
                    return children[0] with { Size = split.Size };
                }

                var total = children.Sum(child => child.Size);
                return split with { Children = [.. children.Select(child => child with { Size = child.Size / total })] };

            default:
                return null;
        }
    }
}
