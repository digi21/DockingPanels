using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace Digi21.WinUI.Docking;

// Centralizes all mutations of the docking layout tree so structural invariants are kept in one
// place: containers are removed when they become empty, split containers with a single remaining
// pane are replaced by that pane, and elements are always detached from their old parent before
// being attached elsewhere.
//
// Every operation works against an IDockSurface or an ILayoutHost rather than against the dock
// site, so the very same code docks a window into the site, into a floating window and into the
// document area. The host an operation acts on is usually derived from the element it is given,
// which keeps callers from having to know where a container lives.
//
// Where an element sits is answered by LayoutTree.Locate(DockSite, UIElement), which walks the
// layout trees themselves. Walking the visual tree instead only works between layout passes: a node
// that has just been moved has no visual parent until the next pass, so the second of two mutations
// made in a single pass would navigate a tree it cannot see.
internal static class LayoutManager
{
    // Docks a window as a new pane at an edge of the whole dock site.
    internal static void DockToSide(DockSite site, DockingWindow window, DockSide side)
    {
        DockToSide((IDockSurface)site, window, side);
    }

    // Docks a window as a new pane at an edge of a docking surface.
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

    // Inserts an existing node as a new pane at an edge of a layout host.
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

    // Docks a window as a new pane beside an existing layout node (container or workspace).
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

    // Inserts a node as a new pane beside an existing layout node.
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

    // Attaches a window as a new tab of an existing container.
    //
    // site: The dock site the window belongs to.
    //
    // window: The window to attach.
    //
    // target: The container that receives the tab.
    //
    // index: The tab position, or -1 to append at the end.
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

    // Moves a tab to another position inside its own container.
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

    // Creates the kind of container a window is hosted in: a tab group for documents, a pane for
    // tool windows.
    private static DockingWindowContainer NewContainerFor(DockingWindow window)
    {
        return window is DocumentWindow ? new DocumentContainer() : new ToolWindowContainer();
    }

    // Opens a document as a tab of the document area's active group, creating the first group when
    // the area is empty.
    internal static void OpenDocument(DocumentHost host, DocumentWindow document)
    {
        // The document area does not know its dock site: it is found through the tree it hangs
        // from, or through the document itself when it has been open before.
        OpenDocument(host.FindSurface()?.Site ?? document.DockSite, host, document);
    }

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

    // Removes the window from its current container as part of a move, without raising closed
    // events, and collapses the abandoned part of the tree.
    private static void Detach(DockSite site, DockingWindow window)
    {
        // Marked as relocating even when the window was closed: otherwise attaching it would
        // announce the opening from inside the container, mid-move, and the operation announces
        // it again once the window is in place. The operation is the single owner of that
        // notification; the container's own stays for windows added to Items directly.
        window.IsRelocating = true;

        if (DetachFromAutoHide(site, window))
        {
            return;
        }

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

    // Takes an auto-hidden window out of the auto-hide machinery, and tells whether it was in it.
    //
    // An auto-hidden window has no container: it belongs to an auto-hide group, and while its flyout
    // is open the flyout's own content host is its visual parent. The container path of Detach is
    // blind to both, so without this a relocation would leave the window parented to the flyout and
    // attaching it anywhere else would fail with the double-parent COMException (0x800F1000).
    private static bool DetachFromAutoHide(DockSite site, DockingWindow window)
    {
        if (window is not ToolWindow tool || site.FindAutoHideGroup(tool) is not { } group)
        {
            return false;
        }

        // Releases the window from the flyout's content host when the flyout is the one showing
        // it. A flyout showing some other window — even one of another group — stays open: the
        // detach has nothing to release there, and closing it would take the panel the user is
        // looking at away because an unrelated window moved.
        site.HideAutoHideFlyoutFor(tool);

        group.Windows.Remove(tool);
        if (group.Windows.Count == 0)
        {
            site.RemoveAutoHideGroup(group);
        }
        else
        {
            site.RefreshAutoHideStrips();
        }

        return true;
    }

    private static void FinishDock(IDockSurface surface, DockingWindow window, bool wasOpen)
    {
        var site = surface.Site;
        window.IsRelocating = false;
        window.DockSite = site;

        // Whatever the window was before the move (floating, or auto-hidden and shown in its
        // flyout), it is docked now. Docking it into a floating window is the exception, and the
        // host puts the state back from SyncWithLayout when the mutation below reaches it.
        window.State = DockingWindowState.Docked;

        site.RegisterWindow(window);
        site.NotifyRelocated(window);
        surface.OnLayoutMutated();

        if (wasOpen)
        {
            site.NotifyLayoutChanged(LayoutChangeKind.WindowDocked);
        }
        else
        {
            site.NotifyWindowOpened(window);
        }

        window.Activate();
    }

    // Collapses a container and all its windows to the nearest auto-hide edge.
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

    // Pins an auto-hide group back into the layout at the edge it collapsed from.
    internal static void DockAutoHideGroup(DockSite site, AutoHideGroup group)
    {
        site.RemoveAutoHideGroup(group);

        RestoreWindows(site, group.Windows, group.RestoreHint, group.Edge);

        site.NotifyLayoutChanged(LayoutChangeKind.WindowDocked);
        group.Windows.FirstOrDefault()?.Activate();
    }

    // Captures where a container sits in the layout tree, so it can be put back there after being
    // auto-hidden or floated out.
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

    // Puts windows back where a restore hint says they came from: into their original container
    // when it is still part of the layout, otherwise into a new pane rebuilt at the remembered
    // position.
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

    // Puts a layout node back where a restore hint says it was. Restoring next to the original
    // neighbor only works while that neighbor is still part of this dock site's layout; otherwise
    // the node docks at the given fallback edge of the host it belongs in.
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

    // Picks where a node lands when its remembered position is gone: documents go back to the
    // document area they belong to, everything else to an edge of the dock site.
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

    // Floats a window out of the layout into its own top-level window, remembering where it was
    // docked so it can be sent back to the same place.
    internal static void FloatWindow(DockSite site, DockingWindow window, RectInt32 bounds)
    {
        // An auto-hidden window has no container to capture a hint from, but its group already
        // carries the one taken when it was unpinned: floating it keeps the way home it had.
        var hint = window.Container is { } previous
            ? CaptureRestoreHint(site, previous, DockSide.Left)
            : site.FindAutoHideGroup(window)?.RestoreHint;

        var wasOpen = window.IsOpen;
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

        // Floating also reopens closed windows, and the operation owns the announcement (see
        // Detach); this path bypasses FinishDock, so it announces here, with the state settled.
        if (!wasOpen)
        {
            site.NotifyWindowOpened(window);
        }

        site.NotifyLayoutChanged(LayoutChangeKind.WindowFloated);
        window.Activate();
    }

    // Docks a whole floating window back into the dock site at the given target, closing it. The
    // windows keep their panes, their tab order and their content.
    internal static void DockFloatingHost(DockSite site, FloatingWindowHost host, DockTarget target)
    {
        DockFloatingHost(site, host, target, site);
    }

    // Docks a whole floating window into a docking surface (the dock site or another floating
    // window) at the given target, closing it.
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
        // and ready to be plugged into the destination, which is also where the foreground goes:
        // the window holding it is the one being closed.
        host.ReleaseAndClose(surface.Root);

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

    // Sends a floating window's tree back where it was floated from: a single pane rejoins the
    // container it left when that container is still there, and a tree of panes is put back as a
    // whole next to the neighbor it sat beside.
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

    // Docks a single window at a resolved drop target of a surface.
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

    // Tells whether the given window is all a surface holds.
    private static bool IsLoneWindowOf(IDockSurface surface, DockingWindow window)
    {
        var windows = LayoutTree.Windows(surface.LayoutChild).ToList();
        return windows.Count == 1 && ReferenceEquals(windows[0], window);
    }

    // Moves windows into a container as part of a relocation, keeping them open.
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

    // Picks the auto-hide edge for an element from its position in the layout tree, like Visual
    // Studio: the orientation of the parent split decides the axis, and the pane's position within
    // it decides the side. Geometry alone is ambiguous, since a pane can touch two dock site edges
    // at once (e.g. a full-width bottom pane also touches the right edge).
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

    // Picks the edge a pane collapses to from where it sits among its siblings: the closer half of
    // the split it belongs to decides the side.
    //
    // orientation: The orientation of the split holding the pane.
    //
    // index: The position of the pane among the panes of that split.
    //
    // count: How many panes the split holds.
    internal static DockSide EdgeOfPane(Orientation orientation, int index, int count)
    {
        var leading = index < count - index - 1;

        return orientation == Orientation.Vertical
            ? (leading ? DockSide.Top : DockSide.Bottom)
            : (leading ? DockSide.Left : DockSide.Right);
    }

    // Removes a window from its container and collapses the tree if needed.
    internal static void RemoveWindow(DockingWindow window)
    {
        if (window.DockSite is { } autoHideSite && DetachFromAutoHide(autoHideSite, window))
        {
            window.IsOpen = false;
            window.IsSelected = false;
            window.State = DockingWindowState.Docked;
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

    // Removes an element from its parent (split container or layout host).
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

    // Collapses a split container that no longer needs to exist: an empty one is removed and one
    // with a single remaining pane is replaced by that pane, which inherits its share of space.
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

    // Replaces an element with another one in its parent, keeping its position.
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

    // Inserts a node as a child of a split container, telling the elements it carries that they
    // have moved.
    private static void InsertChild(DockSite site, SplitContainer split, int index, UIElement node)
    {
        split.Children.Insert(Math.Clamp(index, 0, split.Children.Count), node);
        site.NotifyRelocated(node);
    }

    // Makes a node the root of a host's layout tree, telling the elements it carries that they have
    // moved. Putting back the node that is already there is a no-op: reassigning it would take it
    // out of the XAML tree and put it back within the same layout pass, which is exactly what the
    // notification exists to compensate for.
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
