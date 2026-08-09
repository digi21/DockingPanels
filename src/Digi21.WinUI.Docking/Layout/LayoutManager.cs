using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Centralizes all mutations of the docking layout tree so structural invariants are kept in
/// one place: containers are removed when they become empty, split containers with a single
/// remaining pane are replaced by that pane, and elements are always detached from their old
/// parent before being attached elsewhere.
/// </summary>
/// <remarks>
/// Every operation works against an <see cref="IDockSurface"/> rather than against the dock site,
/// so the very same code docks a window into the site and into a floating window. The surface an
/// operation acts on is usually derived from the element it is given, which keeps callers from
/// having to know which window a container lives in.
/// </remarks>
internal static class LayoutManager
{
    /// <summary>Docks a window as a new pane at an edge of the whole dock site.</summary>
    internal static void DockToSide(DockSite site, ToolWindow window, DockSide side)
    {
        DockToSide((IDockSurface)site, window, side);
    }

    /// <summary>Docks a window as a new pane at an edge of a docking surface.</summary>
    internal static void DockToSide(IDockSurface surface, ToolWindow window, DockSide side)
    {
        var wasOpen = window.IsOpen;
        Detach(window);

        var newContainer = new ToolWindowContainer();
        DockNodeToSide(surface, newContainer, side);
        newContainer.Items.Add(window);
        FinishDock(surface, window, wasOpen);
    }

    /// <summary>Inserts an existing node as a new pane at an edge of a docking surface.</summary>
    private static void DockNodeToSide(IDockSurface surface, FrameworkElement newNode, DockSide side)
    {
        var orientation = side is DockSide.Left or DockSide.Right ? Orientation.Horizontal : Orientation.Vertical;
        var prepend = side is DockSide.Left or DockSide.Top;
        var root = surface.LayoutChild;

        if (root is null)
        {
            surface.LayoutChild = newNode;
        }
        else if (root is SplitContainer rootSplit && rootSplit.Orientation == orientation)
        {
            // Give the new pane a quarter of the resulting space.
            double total = 0;
            foreach (var pane in rootSplit.GetPanes())
            {
                total += SplitContainer.GetEffectiveRelativeSize(pane);
            }

            DockSite.SetRelativeSize(newNode, total / 3.0);

            if (prepend)
            {
                rootSplit.Children.Insert(0, newNode);
            }
            else
            {
                rootSplit.Children.Add(newNode);
            }
        }
        else
        {
            var newSplit = new SplitContainer { Orientation = orientation };
            surface.LayoutChild = null;
            DockSite.SetRelativeSize(root, 3.0);
            DockSite.SetRelativeSize(newNode, 1.0);

            if (prepend)
            {
                newSplit.Children.Add(newNode);
                newSplit.Children.Add(root);
            }
            else
            {
                newSplit.Children.Add(root);
                newSplit.Children.Add(newNode);
            }

            surface.LayoutChild = newSplit;
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

        var surface = targetNode.FindSurface() ?? site;
        var wasOpen = window.IsOpen;
        Detach(window);

        var newContainer = new ToolWindowContainer();
        InsertRelativeTo(newContainer, targetNode, side);
        newContainer.Items.Add(window);
        FinishDock(surface, window, wasOpen);
    }

    /// <summary>Inserts a node as a new pane beside an existing layout node.</summary>
    private static void InsertRelativeTo(FrameworkElement newNode, FrameworkElement targetNode, DockSide side)
    {
        var orientation = side is DockSide.Left or DockSide.Right ? Orientation.Horizontal : Orientation.Vertical;
        var before = side is DockSide.Left or DockSide.Top;
        var targetRelative = SplitContainer.GetEffectiveRelativeSize(targetNode);

        if (VisualTreeHelper.GetParent(targetNode) is SplitContainer parentSplit && parentSplit.Orientation == orientation)
        {
            // Insert beside the target, splitting the target's share in half.
            DockSite.SetRelativeSize(targetNode, targetRelative / 2.0);
            DockSite.SetRelativeSize(newNode, targetRelative / 2.0);

            var index = parentSplit.Children.IndexOf(targetNode);
            parentSplit.Children.Insert(before ? index : index + 1, newNode);
        }
        else
        {
            // Wrap the target in a new split that preserves its share of the parent.
            var newSplit = new SplitContainer { Orientation = orientation };
            DockSite.SetRelativeSize(newSplit, targetRelative);
            ReplaceInParent(targetNode, newSplit);
            DockSite.SetRelativeSize(targetNode, 1.0);
            DockSite.SetRelativeSize(newNode, 1.0);

            if (before)
            {
                newSplit.Children.Add(newNode);
                newSplit.Children.Add(targetNode);
            }
            else
            {
                newSplit.Children.Add(targetNode);
                newSplit.Children.Add(newNode);
            }
        }
    }

    /// <summary>Attaches a window as a new tab of an existing container.</summary>
    internal static void AttachAsTab(DockSite site, ToolWindow window, ToolWindowContainer target)
    {
        if (ReferenceEquals(target, window.Container))
        {
            window.Activate();
            return;
        }

        var surface = target.FindSurface() ?? site;
        var wasOpen = window.IsOpen;
        Detach(window);
        target.Items.Add(window);
        FinishDock(surface, window, wasOpen);
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
            var surface = container.FindSurface();
            container.Items.Remove(window);
            if (container.Items.Count == 0)
            {
                RemoveFromParent(container);
            }

            surface?.OnLayoutMutated();
        }
    }

    private static void FinishDock(IDockSurface surface, ToolWindow window, bool wasOpen)
    {
        var site = surface.Site;
        var wasRelocating = window.IsRelocating;
        window.IsRelocating = false;
        window.DockSite = site;
        site.RegisterWindow(window);
        surface.OnLayoutMutated();

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

        var restoreHint = CaptureRestoreHint(container, edge);

        // Position of the container along the edge, so its tabs land under where it was.
        double offset = 0;
        try
        {
            var reference = (UIElement?)site.LayoutRoot ?? site;
            var origin = container.TransformToVisual(reference).TransformPoint(default);
            offset = edge is DockSide.Left or DockSide.Right ? origin.Y : origin.X;
            if (!double.IsFinite(offset) || offset < 0)
            {
                offset = 0;
            }
        }
        catch (ArgumentException)
        {
            offset = 0;
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

        site.AddAutoHideGroup(new AutoHideGroup(edge, windows, size)
        {
            RestoreHint = restoreHint,
            Offset = offset,
        });
        site.NotifyLayoutChanged(LayoutChangeKind.WindowAutoHidden);
    }

    /// <summary>Pins an auto-hide group back into the layout at the edge it collapsed from.</summary>
    internal static void DockAutoHideGroup(DockSite site, AutoHideGroup group)
    {
        site.RemoveAutoHideGroup(group);

        RestoreWindows(site, group.Windows, group.RestoreHint, group.Edge);

        site.NotifyLayoutChanged(LayoutChangeKind.WindowDocked);
        group.Windows.FirstOrDefault()?.Activate();
    }

    /// <summary>
    /// Captures where a container sits in the layout tree, so it can be put back there after
    /// being auto-hidden or floated out.
    /// </summary>
    internal static DockRestoreHint CaptureRestoreHint(ToolWindowContainer container, DockSide fallbackSide)
    {
        FrameworkElement? sibling = null;
        var side = fallbackSide;

        if (VisualTreeHelper.GetParent(container) is SplitContainer parentSplit)
        {
            var panes = parentSplit.GetPanes();
            var index = panes.IndexOf(container);
            if (index >= 0 && panes.Count > 1)
            {
                var vertical = parentSplit.Orientation == Orientation.Vertical;
                if (index > 0)
                {
                    sibling = panes[index - 1] as FrameworkElement;
                    side = vertical ? DockSide.Bottom : DockSide.Right;
                }
                else
                {
                    sibling = panes[index + 1] as FrameworkElement;
                    side = vertical ? DockSide.Top : DockSide.Left;
                }
            }
        }

        return new DockRestoreHint(container, sibling, side, SplitContainer.GetEffectiveRelativeSize(container));
    }

    /// <summary>
    /// Puts windows back where a restore hint says they came from: into their original
    /// container when it is still part of the layout, otherwise into a new pane rebuilt at
    /// the remembered position.
    /// </summary>
    internal static void RestoreWindows(
        DockSite site,
        IReadOnlyList<ToolWindow> windows,
        DockRestoreHint? hint,
        DockSide fallbackEdge)
    {
        if (hint?.Container is { } original && IsInSite(original, site))
        {
            MoveWindows(windows, original);
            original.FindSurface()?.OnLayoutMutated();
            return;
        }

        var container = new ToolWindowContainer();
        MoveWindows(windows, container);
        RestoreNode(site, container, hint, fallbackEdge);
    }

    /// <summary>
    /// Puts a layout node back where a restore hint says it was. Restoring next to the original
    /// neighbor only works while that neighbor is still part of this dock site's layout;
    /// otherwise the node docks at the given fallback edge of the dock site.
    /// </summary>
    private static void RestoreNode(
        DockSite site,
        FrameworkElement node,
        DockRestoreHint? hint,
        DockSide fallbackEdge)
    {
        if (hint?.Sibling is { } sibling && IsInSite(sibling, site))
        {
            var siblingRelative = SplitContainer.GetEffectiveRelativeSize(sibling);
            InsertRelativeTo(node, sibling, hint.Side);
            DockSite.SetRelativeSize(sibling, siblingRelative);
            DockSite.SetRelativeSize(node, hint.RelativeSize);
            sibling.FindSurface()?.OnLayoutMutated();
        }
        else
        {
            DockNodeToSide(site, node, fallbackEdge);
        }
    }

    /// <summary>Tells whether an element is still part of a layout belonging to the given dock site.</summary>
    private static bool IsInSite(FrameworkElement element, DockSite site)
    {
        return element.FindSurface() is { } surface && ReferenceEquals(surface.Site, site);
    }

    /// <summary>
    /// Floats a window out of the layout into its own top-level window, remembering where it
    /// was docked so it can be sent back to the same place.
    /// </summary>
    internal static void FloatWindow(DockSite site, ToolWindow window, RectInt32 bounds)
    {
        var hint = window.Container is { } previous ? CaptureRestoreHint(previous, DockSide.Left) : null;

        Detach(window);

        var container = new ToolWindowContainer();
        container.Items.Add(window);
        window.IsRelocating = false;
        window.State = DockingWindowState.Floating;

        site.RegisterWindow(window);
        window.DockSite = site;

        var host = new FloatingWindowHost(site, container, bounds) { RestoreHint = hint };
        site.AddFloatingHost(host);

        site.NotifyLayoutChanged(LayoutChangeKind.WindowFloated);
        window.Activate();
    }

    /// <summary>
    /// Docks a whole floating window back into the dock site at the given target, closing it.
    /// The windows keep their panes, their tab order and their content.
    /// </summary>
    internal static void DockFloatingHost(DockSite site, FloatingWindowHost host, DockTarget target)
    {
        DockFloatingHost(site, host, target, site);
    }

    /// <summary>
    /// Docks a whole floating window into a docking surface (the dock site or another floating
    /// window) at the given target, closing it.
    /// </summary>
    internal static void DockFloatingHost(
        DockSite site,
        FloatingWindowHost host,
        DockTarget target,
        IDockSurface surface)
    {
        var tree = host.LayoutElement;
        var windows = host.Windows.ToList();
        if (tree is null || windows.Count == 0 || target.Kind == DockTargetKind.None)
        {
            return;
        }

        var hint = host.RestoreHint;

        // Releases the tree from the floating window and closes it, leaving the tree parentless
        // and ready to be plugged into the destination.
        host.ReleaseAndClose();

        foreach (var window in windows)
        {
            window.State = DockingWindowState.Docked;
        }

        var destination = surface;

        switch (target)
        {
            case { Kind: DockTargetKind.Tab, Element: ToolWindowContainer tabTarget }:
                // Tabs are a flat list, so a tree of panes joins the target container flattened.
                MoveWindows(windows, tabTarget);
                destination = tabTarget.FindSurface() ?? surface;
                break;

            case { Kind: DockTargetKind.Relative, Element: { } node }:
                InsertRelativeTo(tree, node, target.Side);
                destination = node.FindSurface() ?? surface;
                break;

            case { Kind: DockTargetKind.Edge }:
                DockNodeToSide(surface, tree, target.Side);
                break;

            default:
                RestoreHome(site, tree, windows, hint);
                break;
        }

        destination.OnLayoutMutated();
        site.NotifyLayoutChanged(LayoutChangeKind.WindowDocked);
        windows[0].Activate();
    }

    /// <summary>
    /// Sends a floating window's tree back where it was floated from: a single pane rejoins the
    /// container it left when that container is still there, and a tree of panes is put back as a
    /// whole next to the neighbor it sat beside.
    /// </summary>
    private static void RestoreHome(
        DockSite site,
        FrameworkElement tree,
        List<ToolWindow> windows,
        DockRestoreHint? hint)
    {
        if (tree is ToolWindowContainer)
        {
            RestoreWindows(site, windows, hint, DockSide.Left);
            return;
        }

        RestoreNode(site, tree, hint, DockSide.Left);
    }

    /// <summary>Docks a single window at a resolved drop target of a surface.</summary>
    internal static void DockAtTarget(IDockSurface surface, ToolWindow window, DockTarget target)
    {
        if (surface.IsFloating && target.Kind != DockTargetKind.Tab && IsLoneWindowOf(surface, window))
        {
            // The window would be split against a layout made of itself, which would tear down
            // the floating window it is being dropped on.
            return;
        }

        switch (target)
        {
            case { Kind: DockTargetKind.Edge }:
                DockToSide(surface, window, target.Side);
                break;
            case { Kind: DockTargetKind.Relative, Element: { } node }:
                DockRelativeTo(surface.Site, window, node, target.Side);
                break;
            case { Kind: DockTargetKind.Tab, Element: ToolWindowContainer container }:
                AttachAsTab(surface.Site, window, container);
                break;
        }
    }

    /// <summary>Tells whether the given window is all a surface holds.</summary>
    private static bool IsLoneWindowOf(IDockSurface surface, ToolWindow window)
    {
        var windows = LayoutTree.Windows(surface.LayoutChild).ToList();
        return windows.Count == 1 && ReferenceEquals(windows[0], window);
    }

    /// <summary>Moves windows into a container as part of a relocation, keeping them open.</summary>
    private static void MoveWindows(IReadOnlyList<ToolWindow> windows, ToolWindowContainer container)
    {
        foreach (var window in windows)
        {
            window.IsRelocating = true;
            container.Items.Add(window);
            window.IsRelocating = false;
        }
    }

    /// <summary>
    /// Picks the auto-hide edge for an element from its position in the layout tree, like
    /// Visual Studio: the orientation of the parent split decides the axis, and the pane's
    /// position within it decides the side. Geometry alone is ambiguous, since a pane can
    /// touch two dock site edges at once (e.g. a full-width bottom pane also touches the right edge).
    /// </summary>
    private static DockSide NearestEdge(DockSite site, FrameworkElement element)
    {
        _ = site;

        if (VisualTreeHelper.GetParent(element) is SplitContainer parent)
        {
            var panes = parent.GetPanes();
            var index = panes.IndexOf(element);
            if (index < 0)
            {
                return DockSide.Left;
            }

            var leading = index < panes.Count - index - 1;

            return parent.Orientation == Orientation.Vertical
                ? (leading ? DockSide.Top : DockSide.Bottom)
                : (leading ? DockSide.Left : DockSide.Right);
        }

        return DockSide.Left;
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
        var surface = container.FindSurface();

        container.Items.Remove(tool);
        tool.State = DockingWindowState.Docked;

        if (container.Items.Count == 0)
        {
            RemoveFromParent(container);
        }

        // A floating window that has just lost its last window closes with it.
        surface?.OnLayoutMutated();
    }

    /// <summary>Removes an element from its parent (split container or surface root).</summary>
    private static void RemoveFromParent(FrameworkElement element)
    {
        if (VisualTreeHelper.GetParent(element) is SplitContainer split)
        {
            split.Children.Remove(element);
            CollapseSplit(split);
        }
        else if (element.FindSurface() is { } surface && ReferenceEquals(surface.LayoutChild, element))
        {
            surface.LayoutChild = null;
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
        else if (old.FindSurface() is { } surface && ReferenceEquals(surface.LayoutChild, old))
        {
            surface.LayoutChild = replacement;
        }
    }
}
