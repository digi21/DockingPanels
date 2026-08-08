using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    /// <summary>Docks a window as a new pane at an edge of the whole dock site.</summary>
    internal static void DockToSide(DockSite site, ToolWindow window, DockSide side)
    {
        var wasOpen = window.IsOpen;
        Detach(window);

        var newContainer = new ToolWindowContainer();
        DockContainerToSide(site, newContainer, side);
        newContainer.Items.Add(window);
        FinishDock(site, window, wasOpen);
    }

    /// <summary>Inserts an existing container as a new pane at an edge of the dock site.</summary>
    private static void DockContainerToSide(DockSite site, ToolWindowContainer newContainer, DockSide side)
    {
        var orientation = side is DockSide.Left or DockSide.Right ? Orientation.Horizontal : Orientation.Vertical;
        var prepend = side is DockSide.Left or DockSide.Top;
        var root = site.Child;

        if (root is null)
        {
            site.Child = newContainer;
        }
        else if (root is SplitContainer rootSplit && rootSplit.Orientation == orientation)
        {
            // Give the new pane a quarter of the resulting space.
            double total = 0;
            foreach (var pane in rootSplit.GetPanes())
            {
                total += SplitContainer.GetEffectiveRelativeSize(pane);
            }

            DockSite.SetRelativeSize(newContainer, total / 3.0);

            if (prepend)
            {
                rootSplit.Children.Insert(0, newContainer);
            }
            else
            {
                rootSplit.Children.Add(newContainer);
            }
        }
        else
        {
            var newSplit = new SplitContainer { Orientation = orientation };
            site.Child = null;
            DockSite.SetRelativeSize(root, 3.0);
            DockSite.SetRelativeSize(newContainer, 1.0);

            if (prepend)
            {
                newSplit.Children.Add(newContainer);
                newSplit.Children.Add(root);
            }
            else
            {
                newSplit.Children.Add(root);
                newSplit.Children.Add(newContainer);
            }

            site.Child = newSplit;
        }
    }

    /// <summary>Docks a window as a new pane beside an existing layout node (container or workspace).</summary>
    internal static void DockRelativeTo(DockSite site, ToolWindow window, FrameworkElement targetNode, DockSide side)
    {
        if (window.Container is { } own && ReferenceEquals(targetNode, own) && own.Items.Count == 1)
        {
            // Splitting a container that only holds the dragged window is a no-op.
            return;
        }

        var wasOpen = window.IsOpen;
        Detach(window);

        var newContainer = new ToolWindowContainer();
        var orientation = side is DockSide.Left or DockSide.Right ? Orientation.Horizontal : Orientation.Vertical;
        var before = side is DockSide.Left or DockSide.Top;
        var targetRelative = SplitContainer.GetEffectiveRelativeSize(targetNode);

        if (VisualTreeHelper.GetParent(targetNode) is SplitContainer parentSplit && parentSplit.Orientation == orientation)
        {
            // Insert beside the target, splitting the target's share in half.
            DockSite.SetRelativeSize(targetNode, targetRelative / 2.0);
            DockSite.SetRelativeSize(newContainer, targetRelative / 2.0);

            var index = parentSplit.Children.IndexOf(targetNode);
            parentSplit.Children.Insert(before ? index : index + 1, newContainer);
        }
        else
        {
            // Wrap the target in a new split that preserves its share of the parent.
            var newSplit = new SplitContainer { Orientation = orientation };
            DockSite.SetRelativeSize(newSplit, targetRelative);
            ReplaceInParent(targetNode, newSplit);
            DockSite.SetRelativeSize(targetNode, 1.0);
            DockSite.SetRelativeSize(newContainer, 1.0);

            if (before)
            {
                newSplit.Children.Add(newContainer);
                newSplit.Children.Add(targetNode);
            }
            else
            {
                newSplit.Children.Add(targetNode);
                newSplit.Children.Add(newContainer);
            }
        }

        newContainer.Items.Add(window);
        FinishDock(site, window, wasOpen);
    }

    /// <summary>Attaches a window as a new tab of an existing container.</summary>
    internal static void AttachAsTab(DockSite site, ToolWindow window, ToolWindowContainer target)
    {
        if (ReferenceEquals(target, window.Container))
        {
            window.Activate();
            return;
        }

        var wasOpen = window.IsOpen;
        Detach(window);
        target.Items.Add(window);
        FinishDock(site, window, wasOpen);
    }

    /// <summary>
    /// Removes the window from its current container as part of a move, without raising
    /// closed events, and collapses the abandoned part of the tree.
    /// </summary>
    private static void Detach(ToolWindow window)
    {
        window.IsRelocating = window.IsOpen;

        if (window.Container is { } container)
        {
            container.Items.Remove(window);
            if (container.Items.Count == 0)
            {
                RemoveFromParent(container);
            }
        }
    }

    private static void FinishDock(DockSite site, ToolWindow window, bool wasOpen)
    {
        var wasRelocating = window.IsRelocating;
        window.IsRelocating = false;
        window.DockSite = site;
        site.RegisterWindow(window);

        if (wasRelocating || wasOpen)
        {
            site.NotifyLayoutChanged(LayoutChangeKind.WindowDocked);
        }
        else
        {
            site.NotifyWindowOpened(window);
        }

        window.Activate();
    }

    /// <summary>Collapses a container and all its windows to the nearest auto-hide edge.</summary>
    internal static void AutoHideContainer(DockSite site, ToolWindowContainer container)
    {
        var edge = NearestEdge(site, container);
        var horizontalEdge = edge is DockSide.Left or DockSide.Right;
        var size = horizontalEdge ? container.ActualWidth : container.ActualHeight;
        if (!double.IsFinite(size) || size < 100)
        {
            size = 300;
        }

        var windows = container.Items.ToList();
        foreach (var window in windows)
        {
            window.IsRelocating = true;
        }

        while (container.Items.Count > 0)
        {
            container.Items.RemoveAt(container.Items.Count - 1);
        }

        RemoveFromParent(container);

        foreach (var window in windows)
        {
            window.IsRelocating = false;
            window.State = DockingWindowState.AutoHide;
            window.IsOpen = true;

            if (ReferenceEquals(site.ActiveWindow, window))
            {
                site.SetActiveWindow(null);
            }
        }

        site.AddAutoHideGroup(new AutoHideGroup(edge, windows, size));
        site.NotifyLayoutChanged(LayoutChangeKind.WindowAutoHidden);
    }

    /// <summary>Pins an auto-hide group back into the layout at the edge it collapsed from.</summary>
    internal static void DockAutoHideGroup(DockSite site, AutoHideGroup group)
    {
        site.RemoveAutoHideGroup(group);

        var container = new ToolWindowContainer();
        foreach (var window in group.Windows)
        {
            window.IsRelocating = true;
            container.Items.Add(window);
            window.IsRelocating = false;
        }

        DockContainerToSide(site, container, group.Edge);
        site.NotifyLayoutChanged(LayoutChangeKind.WindowDocked);
        group.Windows.FirstOrDefault()?.Activate();
    }

    /// <summary>Finds the dock site edge geometrically nearest to the given element.</summary>
    private static DockSide NearestEdge(DockSite site, FrameworkElement element)
    {
        try
        {
            var bounds = element
                .TransformToVisual(site)
                .TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));

            var distances = new (DockSide Side, double Distance)[]
            {
                (DockSide.Left, bounds.Left),
                (DockSide.Top, bounds.Top),
                (DockSide.Right, site.ActualWidth - bounds.Right),
                (DockSide.Bottom, site.ActualHeight - bounds.Bottom),
            };

            return distances.MinBy(d => d.Distance).Side;
        }
        catch (ArgumentException)
        {
            return DockSide.Left;
        }
    }

    /// <summary>Removes a window from its container and collapses the tree if needed.</summary>
    internal static void RemoveWindow(DockingWindow window)
    {
        if (window is not ToolWindow tool)
        {
            return;
        }

        if (tool.State == DockingWindowState.AutoHide && tool.DockSite is { } site
            && site.FindAutoHideGroup(tool) is { } group)
        {
            site.HideAutoHideFlyout();
            group.Windows.Remove(tool);
            if (group.Windows.Count == 0)
            {
                site.RemoveAutoHideGroup(group);
            }
            else
            {
                site.RefreshAutoHideStrips();
            }

            tool.IsOpen = false;
            tool.IsSelected = false;
            tool.State = DockingWindowState.Docked;
            return;
        }

        if (tool.Container is null)
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
