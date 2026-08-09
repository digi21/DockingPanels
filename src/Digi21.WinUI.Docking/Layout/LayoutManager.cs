using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Centralizes all mutations of the docking layout tree so structural invariants are kept in
/// one place: containers are removed when they become empty, split containers with a single
/// remaining pane are replaced by that pane, and elements are always detached from their old
/// parent before being attached elsewhere.
/// </summary>
/// <remarks>
/// <para>
/// Every operation works against an <see cref="IDockSurface"/> or an <see cref="ILayoutHost"/>
/// rather than against the dock site, so the very same code docks a window into the site, into a
/// floating window and into the document area. The host an operation acts on is usually derived
/// from the element it is given, which keeps callers from having to know where a container lives.
/// </para>
/// <para>
/// Where an element sits is answered by <see cref="LayoutTree.Locate(DockSite, UIElement)"/>,
/// which walks the layout trees themselves. Walking the visual tree instead only works between
/// layout passes: a node that has just been moved has no visual parent until the next pass, so
/// the second of two mutations made in a single pass would navigate a tree it cannot see.
/// </para>
/// </remarks>
internal static class LayoutManager
{
    /// <summary>Docks a window as a new pane at an edge of the whole dock site.</summary>
    internal static void DockToSide(DockSite site, DockingWindow window, DockSide side)
    {
        DockToSide((IDockSurface)site, window, side);
    }

    /// <summary>Docks a window as a new pane at an edge of a docking surface.</summary>
    internal static void DockToSide(IDockSurface surface, DockingWindow window, DockSide side)
    {
        var site = surface.Site;
        var wasOpen = window.IsOpen;
        Detach(site, window);

        var newContainer = NewContainerFor(window);
        DockNodeToSide(site, surface, newContainer, side);
        newContainer.Items.Add(window);
        FinishDock(surface, window, wasOpen);
    }

    /// <summary>Inserts an existing node as a new pane at an edge of a layout host.</summary>
    private static void DockNodeToSide(DockSite site, ILayoutHost host, FrameworkElement newNode, DockSide side)
    {
        var orientation = side is DockSide.Left or DockSide.Right ? Orientation.Horizontal : Orientation.Vertical;
        var prepend = side is DockSide.Left or DockSide.Top;
        var root = host.LayoutChild;

        if (root is null)
        {
            SetLayoutChild(site, host, newNode);
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

            InsertChild(site, rootSplit, prepend ? 0 : rootSplit.Children.Count, newNode);
        }
        else
        {
            var newSplit = new SplitContainer { Orientation = orientation };
            host.LayoutChild = null;
            DockSite.SetRelativeSize(root, 3.0);
            DockSite.SetRelativeSize(newNode, 1.0);

            if (prepend)
            {
                InsertChild(site, newSplit, 0, newNode);
                InsertChild(site, newSplit, 1, root);
            }
            else
            {
                InsertChild(site, newSplit, 0, root);
                InsertChild(site, newSplit, 1, newNode);
            }

            SetLayoutChild(site, host, newSplit);
        }
    }

    /// <summary>Docks a window as a new pane beside an existing layout node (container or workspace).</summary>
    internal static void DockRelativeTo(DockSite site, DockingWindow window, FrameworkElement targetNode, DockSide side)
    {
        if (window.Container is { } own && ReferenceEquals(targetNode, own) && own.Items.Count == 1)
        {
            // Splitting a container that only holds the dragged window is a no-op.
            return;
        }

        var surface = LayoutTree.Locate(site, targetNode)?.Surface ?? site;
        var wasOpen = window.IsOpen;
        Detach(site, window);

        var newContainer = NewContainerFor(window);
        InsertRelativeTo(site, newContainer, targetNode, side);
        newContainer.Items.Add(window);
        FinishDock(surface, window, wasOpen);
    }

    /// <summary>Inserts a node as a new pane beside an existing layout node.</summary>
    private static void InsertRelativeTo(DockSite site, FrameworkElement newNode, FrameworkElement targetNode, DockSide side)
    {
        var orientation = side is DockSide.Left or DockSide.Right ? Orientation.Horizontal : Orientation.Vertical;
        var before = side is DockSide.Left or DockSide.Top;
        var targetRelative = SplitContainer.GetEffectiveRelativeSize(targetNode);
        var location = LayoutTree.Locate(site, targetNode);

        if (location is { Split: { } parentSplit } && parentSplit.Orientation == orientation)
        {
            // Insert beside the target, splitting the target's share in half.
            DockSite.SetRelativeSize(targetNode, targetRelative / 2.0);
            DockSite.SetRelativeSize(newNode, targetRelative / 2.0);

            var index = parentSplit.Children.IndexOf(targetNode);
            InsertChild(site, parentSplit, before ? index : index + 1, newNode);
        }
        else
        {
            // Wrap the target in a new split that preserves its share of the parent.
            var newSplit = new SplitContainer { Orientation = orientation };
            DockSite.SetRelativeSize(newSplit, targetRelative);
            ReplaceInParent(site, targetNode, newSplit);
            DockSite.SetRelativeSize(targetNode, 1.0);
            DockSite.SetRelativeSize(newNode, 1.0);

            if (before)
            {
                InsertChild(site, newSplit, 0, newNode);
                InsertChild(site, newSplit, 1, targetNode);
            }
            else
            {
                InsertChild(site, newSplit, 0, targetNode);
                InsertChild(site, newSplit, 1, newNode);
            }
        }
    }

    /// <summary>Attaches a window as a new tab of an existing container.</summary>
    /// <param name="site">The dock site the window belongs to.</param>
    /// <param name="window">The window to attach.</param>
    /// <param name="target">The container that receives the tab.</param>
    /// <param name="index">The tab position, or -1 to append at the end.</param>
    internal static void AttachAsTab(DockSite site, DockingWindow window, DockingWindowContainer target, int index = -1)
    {
        if (ReferenceEquals(target, window.Container))
        {
            Reorder(target, window, index);
            window.Activate();
            return;
        }

        var surface = LayoutTree.Locate(site, target)?.Surface ?? site;
        var wasOpen = window.IsOpen;
        Detach(site, window);
        target.Items.Insert(ClampIndex(target, index), window);
        FinishDock(surface, window, wasOpen);
    }

    /// <summary>Moves a tab to another position inside its own container.</summary>
    private static void Reorder(DockingWindowContainer container, DockingWindow window, int index)
    {
        var from = container.Items.IndexOf(window);
        if (from < 0 || index < 0)
        {
            return;
        }

        // The insertion index counts the window itself while it is still in the list.
        var to = Math.Clamp(index > from ? index - 1 : index, 0, container.Items.Count - 1);
        if (to == from)
        {
            return;
        }

        window.IsRelocating = true;
        container.Items.RemoveAt(from);
        container.Items.Insert(to, window);
        window.IsRelocating = false;
        container.Select(window);
        window.DockSite?.NotifyLayoutChanged(LayoutChangeKind.WindowDocked);
    }

    private static int ClampIndex(DockingWindowContainer container, int index)
    {
        return index < 0 ? container.Items.Count : Math.Clamp(index, 0, container.Items.Count);
    }

    /// <summary>Creates the kind of container a window is hosted in: a tab group for documents, a pane for tool windows.</summary>
    private static DockingWindowContainer NewContainerFor(DockingWindow window)
    {
        return window is DocumentWindow ? new DocumentContainer() : new ToolWindowContainer();
    }

    /// <summary>
    /// Opens a document as a tab of the document area's active group, creating the first group
    /// when the area is empty.
    /// </summary>
    internal static void OpenDocument(DocumentHost host, DocumentWindow document)
    {
        // The document area does not know its dock site: it is found through the tree it hangs
        // from, or through the document itself when it has been open before.
        OpenDocument(host.FindSurface()?.Site ?? document.DockSite, host, document);
    }

    /// <inheritdoc cref="OpenDocument(DocumentHost, DocumentWindow)"/>
    internal static void OpenDocument(DockSite? site, DocumentHost host, DocumentWindow document)
    {
        if (site is null || LayoutTree.Locate(site, host)?.Surface is not { } surface)
        {
            return;
        }

        if (host.ActiveGroup is { } group)
        {
            AttachAsTab(site, document, group);
            return;
        }

        var wasOpen = document.IsOpen;
        Detach(site, document);

        var container = new DocumentContainer();
        DockNodeToSide(site, host, container, DockSide.Left);
        container.Items.Add(document);
        FinishDock(surface, document, wasOpen);
    }

    /// <summary>
    /// Removes the window from its current container as part of a move, without raising
    /// closed events, and collapses the abandoned part of the tree.
    /// </summary>
    private static void Detach(DockSite site, DockingWindow window)
    {
        window.IsRelocating = window.IsOpen;

        if (window.Container is { } container)
        {
            var surface = LayoutTree.Locate(site, container)?.Surface;
            container.Items.Remove(window);
            if (container.Items.Count == 0)
            {
                RemoveFromParent(site, container);
            }

            surface?.OnLayoutMutated();
        }
    }

    private static void FinishDock(IDockSurface surface, DockingWindow window, bool wasOpen)
    {
        var site = surface.Site;
        var wasRelocating = window.IsRelocating;
        window.IsRelocating = false;
        window.DockSite = site;
        site.RegisterWindow(window);
        site.NotifyRelocated(window);
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
    internal static void AutoHideContainer(DockSite site, DockingWindowContainer container)
    {
        var windows = container.Items.OfType<ToolWindow>().ToList();
        if (windows.Count != container.Items.Count)
        {
            // Only tool windows auto-hide; a group holding anything else stays where it is.
            return;
        }

        var edge = NearestEdge(site, container);
        var horizontalEdge = edge is DockSide.Left or DockSide.Right;
        var size = horizontalEdge ? container.ActualWidth : container.ActualHeight;
        if (!double.IsFinite(size) || size < 100)
        {
            size = 300;
        }

        var restoreHint = CaptureRestoreHint(site, container, edge);

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

        foreach (var window in windows)
        {
            window.IsRelocating = true;
        }

        while (container.Items.Count > 0)
        {
            container.Items.RemoveAt(container.Items.Count - 1);
        }

        RemoveFromParent(site, container);

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
    internal static DockRestoreHint CaptureRestoreHint(DockSite site, DockingWindowContainer container, DockSide fallbackSide)
    {
        FrameworkElement? sibling = null;
        var side = fallbackSide;

        if (LayoutTree.Locate(site, container) is { Split: { } parentSplit })
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
        IReadOnlyList<DockingWindow> windows,
        DockRestoreHint? hint,
        DockSide fallbackEdge)
    {
        if (windows.Count == 0)
        {
            return;
        }

        if (hint?.Container is { } original && LayoutTree.Locate(site, original) is { } location)
        {
            MoveWindows(site, windows, original);
            location.Surface.OnLayoutMutated();
            return;
        }

        var container = NewContainerFor(windows[0]);
        MoveWindows(site, windows, container);
        RestoreNode(site, container, hint, fallbackEdge);
    }

    /// <summary>
    /// Puts a layout node back where a restore hint says it was. Restoring next to the original
    /// neighbor only works while that neighbor is still part of this dock site's layout;
    /// otherwise the node docks at the given fallback edge of the host it belongs in.
    /// </summary>
    private static void RestoreNode(
        DockSite site,
        FrameworkElement node,
        DockRestoreHint? hint,
        DockSide fallbackEdge)
    {
        if (hint?.Sibling is { } sibling && LayoutTree.Locate(site, sibling) is { } location)
        {
            var siblingRelative = SplitContainer.GetEffectiveRelativeSize(sibling);
            InsertRelativeTo(site, node, sibling, hint.Side);
            DockSite.SetRelativeSize(sibling, siblingRelative);
            DockSite.SetRelativeSize(node, hint.RelativeSize);
            location.Surface.OnLayoutMutated();
        }
        else
        {
            DockNodeToSide(site, FallbackHost(site, node), node, fallbackEdge);
        }
    }

    /// <summary>
    /// Picks where a node lands when its remembered position is gone: documents go back to the
    /// document area they belong to, everything else to an edge of the dock site.
    /// </summary>
    private static ILayoutHost FallbackHost(DockSite site, FrameworkElement node)
    {
        var windows = LayoutTree.Windows(node).ToList();
        if (windows.Count > 0
            && windows.All(w => w is DocumentWindow)
            && LayoutTree.DocumentHosts(site.Child).FirstOrDefault() is { } host)
        {
            return host;
        }

        return site;
    }

    /// <summary>
    /// Floats a window out of the layout into its own top-level window, remembering where it
    /// was docked so it can be sent back to the same place.
    /// </summary>
    internal static void FloatWindow(DockSite site, DockingWindow window, RectInt32 bounds)
    {
        var hint = window.Container is { } previous ? CaptureRestoreHint(site, previous, DockSide.Left) : null;

        Detach(site, window);

        var container = NewContainerFor(window);
        container.Items.Add(window);
        window.IsRelocating = false;
        window.State = DockingWindowState.Floating;

        site.RegisterWindow(window);
        window.DockSite = site;

        var host = new FloatingWindowHost(site, container, bounds) { RestoreHint = hint };
        site.AddFloatingHost(host);
        site.NotifyRelocated(window);

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
            case { Kind: DockTargetKind.Tab, Element: DockingWindowContainer tabTarget }:
                // Tabs are a flat list, so a tree of panes joins the target container flattened.
                MoveWindows(site, windows, tabTarget, target.Index);
                destination = LayoutTree.Locate(site, tabTarget)?.Surface ?? surface;
                break;

            case { Kind: DockTargetKind.Tab, Element: DocumentHost emptyArea }:
                DockNodeToSide(site, emptyArea, tree, DockSide.Left);
                destination = LayoutTree.Locate(site, emptyArea)?.Surface ?? surface;
                break;

            case { Kind: DockTargetKind.Relative, Element: { } node }:
                InsertRelativeTo(site, tree, node, target.Side);
                destination = LayoutTree.Locate(site, node)?.Surface ?? surface;
                break;

            case { Kind: DockTargetKind.Edge }:
                DockNodeToSide(site, surface, tree, target.Side);
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
        List<DockingWindow> windows,
        DockRestoreHint? hint)
    {
        if (tree is DockingWindowContainer)
        {
            RestoreWindows(site, windows, hint, DockSide.Left);
            return;
        }

        RestoreNode(site, tree, hint, DockSide.Left);
    }

    /// <summary>Docks a single window at a resolved drop target of a surface.</summary>
    internal static void DockAtTarget(IDockSurface surface, DockingWindow window, DockTarget target)
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
            case { Kind: DockTargetKind.Tab, Element: DockingWindowContainer container }:
                AttachAsTab(surface.Site, window, container, target.Index);
                break;
            case { Kind: DockTargetKind.Tab, Element: DocumentHost host } when window is DocumentWindow document:
                OpenDocument(surface.Site, host, document);
                break;
        }
    }

    /// <summary>Tells whether the given window is all a surface holds.</summary>
    private static bool IsLoneWindowOf(IDockSurface surface, DockingWindow window)
    {
        var windows = LayoutTree.Windows(surface.LayoutChild).ToList();
        return windows.Count == 1 && ReferenceEquals(windows[0], window);
    }

    /// <summary>Moves windows into a container as part of a relocation, keeping them open.</summary>
    private static void MoveWindows(
        DockSite site,
        IReadOnlyList<DockingWindow> windows,
        DockingWindowContainer container,
        int index = -1)
    {
        var position = ClampIndex(container, index);

        foreach (var window in windows)
        {
            window.IsRelocating = true;
            container.Items.Insert(Math.Clamp(position, 0, container.Items.Count), window);
            window.IsRelocating = false;
            site.NotifyRelocated(window);
            position++;
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
        if (LayoutTree.Locate(site, element) is not { Split: { } parent })
        {
            return DockSide.Left;
        }

        var panes = parent.GetPanes();
        var index = panes.IndexOf(element);

        return index < 0 ? DockSide.Left : EdgeOfPane(parent.Orientation, index, panes.Count);
    }

    /// <summary>
    /// Picks the edge a pane collapses to from where it sits among its siblings: the closer half
    /// of the split it belongs to decides the side.
    /// </summary>
    /// <param name="orientation">The orientation of the split holding the pane.</param>
    /// <param name="index">The position of the pane among the panes of that split.</param>
    /// <param name="count">How many panes the split holds.</param>
    internal static DockSide EdgeOfPane(Orientation orientation, int index, int count)
    {
        var leading = index < count - index - 1;

        return orientation == Orientation.Vertical
            ? (leading ? DockSide.Top : DockSide.Bottom)
            : (leading ? DockSide.Left : DockSide.Right);
    }

    /// <summary>Removes a window from its container and collapses the tree if needed.</summary>
    internal static void RemoveWindow(DockingWindow window)
    {
        if (window is ToolWindow tool && tool.State == DockingWindowState.AutoHide
            && tool.DockSite is { } autoHideSite && autoHideSite.FindAutoHideGroup(tool) is { } group)
        {
            autoHideSite.HideAutoHideFlyout();
            group.Windows.Remove(tool);
            if (group.Windows.Count == 0)
            {
                autoHideSite.RemoveAutoHideGroup(group);
            }
            else
            {
                autoHideSite.RefreshAutoHideStrips();
            }

            tool.IsOpen = false;
            tool.IsSelected = false;
            tool.State = DockingWindowState.Docked;
            return;
        }

        if (window.Container is not { } container)
        {
            return;
        }

        var site = window.DockSite;
        var surface = site is null ? container.FindSurface() : LayoutTree.Locate(site, container)?.Surface;

        container.Items.Remove(window);
        window.State = DockingWindowState.Docked;

        if (container.Items.Count == 0 && site is not null)
        {
            RemoveFromParent(site, container);
        }

        // A floating window that has just lost its last window closes with it.
        surface?.OnLayoutMutated();
    }

    /// <summary>Removes an element from its parent (split container or layout host).</summary>
    private static void RemoveFromParent(DockSite site, FrameworkElement element)
    {
        if (LayoutTree.Locate(site, element) is not { } location)
        {
            return;
        }

        if (location.Split is { } split)
        {
            split.Children.Remove(element);
            CollapseSplit(site, split);
        }
        else if (ReferenceEquals(location.Host.LayoutChild, element))
        {
            location.Host.LayoutChild = null;
        }
    }

    /// <summary>
    /// Collapses a split container that no longer needs to exist: an empty one is removed and
    /// one with a single remaining pane is replaced by that pane, which inherits its share of space.
    /// </summary>
    private static void CollapseSplit(DockSite site, SplitContainer split)
    {
        var panes = split.GetPanes();
        if (panes.Count > 1)
        {
            return;
        }

        if (panes.Count == 0)
        {
            RemoveFromParent(site, split);
            return;
        }

        var lone = panes[0];
        DockSite.SetRelativeSize(lone, DockSite.GetRelativeSize(split));
        split.Children.Remove(lone);
        ReplaceInParent(site, split, lone);
    }

    /// <summary>Replaces an element with another one in its parent, keeping its position.</summary>
    private static void ReplaceInParent(DockSite site, FrameworkElement old, UIElement replacement)
    {
        if (ReferenceEquals(old, replacement) || LayoutTree.Locate(site, old) is not { } location)
        {
            return;
        }

        if (location.Split is { } parentSplit)
        {
            var index = parentSplit.Children.IndexOf(old);
            parentSplit.Children.RemoveAt(index);
            InsertChild(site, parentSplit, index, replacement);
        }
        else if (ReferenceEquals(location.Host.LayoutChild, old))
        {
            SetLayoutChild(site, location.Host, replacement);
        }
    }

    /// <summary>
    /// Inserts a node as a child of a split container, telling the elements it carries that they
    /// have moved.
    /// </summary>
    private static void InsertChild(DockSite site, SplitContainer split, int index, UIElement node)
    {
        split.Children.Insert(Math.Clamp(index, 0, split.Children.Count), node);
        site.NotifyRelocated(node);
    }

    /// <summary>
    /// Makes a node the root of a host's layout tree, telling the elements it carries that they
    /// have moved. Putting back the node that is already there is a no-op: reassigning it would
    /// take it out of the XAML tree and put it back within the same layout pass, which is exactly
    /// what the notification exists to compensate for.
    /// </summary>
    private static void SetLayoutChild(DockSite site, ILayoutHost host, UIElement? node)
    {
        if (ReferenceEquals(host.LayoutChild, node))
        {
            return;
        }

        host.LayoutChild = node;

        if (node is not null)
        {
            site.NotifyRelocated(node);
        }
    }
}
