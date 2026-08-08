using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Centralizes all mutations of the docking layout tree so structural invariants are kept in
/// one place: containers are removed when they become empty, split containers with a single
/// remaining pane are replaced by that pane, and elements are always detached from their old
/// parent before being attached elsewhere.
/// </summary>
internal static class LayoutManager
{
    /// <summary>Removes a window from its container and collapses the tree if needed.</summary>
    internal static void RemoveWindow(DockingWindow window)
    {
        if (window is not ToolWindow tool || tool.Container is null)
        {
            return;
        }

        var container = tool.Container;
        container.Items.Remove(tool);

        if (container.Items.Count == 0)
        {
            RemoveFromParent(container);
        }
    }

    /// <summary>Removes an element from its parent (split container or dock site root).</summary>
    private static void RemoveFromParent(FrameworkElement element)
    {
        if (VisualTreeHelper.GetParent(element) is SplitContainer split)
        {
            split.Children.Remove(element);
            CollapseSplit(split);
        }
        else if (element.FindAncestor<DockSite>() is { } site && ReferenceEquals(site.Child, element))
        {
            site.Child = null;
        }
    }

    /// <summary>
    /// Collapses a split container that no longer needs to exist: an empty one is removed and
    /// one with a single remaining pane is replaced by that pane, which inherits its share of space.
    /// </summary>
    private static void CollapseSplit(SplitContainer split)
    {
        var panes = split.GetPanes();
        if (panes.Count > 1)
        {
            return;
        }

        if (panes.Count == 0)
        {
            RemoveFromParent(split);
            return;
        }

        var lone = panes[0];
        DockSite.SetRelativeSize(lone, DockSite.GetRelativeSize(split));
        split.Children.Remove(lone);
        ReplaceInParent(split, lone);
    }

    /// <summary>Replaces an element with another one in its parent, keeping its position.</summary>
    private static void ReplaceInParent(FrameworkElement old, UIElement replacement)
    {
        if (VisualTreeHelper.GetParent(old) is SplitContainer parentSplit)
        {
            var index = parentSplit.Children.IndexOf(old);
            parentSplit.Children.RemoveAt(index);
            parentSplit.Children.Insert(index, replacement);
        }
        else if (old.FindAncestor<DockSite>() is { } site && ReferenceEquals(site.Child, old))
        {
            site.Child = replacement;
        }
    }
}
