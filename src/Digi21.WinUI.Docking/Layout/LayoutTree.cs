using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

// Reads a docking layout tree. The tree of a dock site, the tree of a floating window and the tree
// inside a document host have the same shape (splits, containers, the workspace and the document
// area), so the same walks serve all of them.
internal static class LayoutTree
{
    // Finds where a node sits in the layouts of a dock site: which surface and layout host own the
    // branch it belongs to, and which split container holds it, if any.
    //
    // The search walks the layout trees themselves rather than the visual tree. A node that has
    // just been moved is detached from its old visual parent and will only be attached to the new
    // one on the next layout pass, so VisualTreeHelper reports a null parent for it and for
    // everything below it in between. Two mutations in the same layout pass would make the second
    // one navigate a tree it cannot see, which is how an auto-hidden pane used to end up at the
    // wrong edge.
    internal static LayoutLocation? Locate(DockSite site, UIElement node)
    {
        if (Locate(site, site, site.Child, null, node) is { } inSite)
        {
            return inSite;
        }

        foreach (var host in site.FloatingHosts)
        {
            if (Locate(host.Surface, host.Surface, host.LayoutChild, null, node) is { } inFloatingWindow)
            {
                return inFloatingWindow;
            }
        }

        return null;
    }

    private static LayoutLocation? Locate(
        IDockSurface surface,
        ILayoutHost host,
        UIElement? current,
        SplitContainer? split,
        UIElement node)
    {
        if (current is null)
        {
            return null;
        }

        if (ReferenceEquals(current, node))
        {
            return new LayoutLocation(surface, host, split);
        }

        switch (current)
        {
            case SplitContainer container:
                foreach (var pane in container.GetPanes())
                {
                    if (Locate(surface, host, pane, container, node) is { } found)
                    {
                        return found;
                    }
                }

                break;

            // A document area owns a layout tree of its own, so it is the host of everything below it.
            case DocumentHost documentHost:
                return Locate(surface, documentHost, documentHost.Child, null, node);
        }

        return null;
    }

    // Enumerates the elements of a moved subtree whose content the application owns, and which
    // therefore need to hear about the move: the windows, the workspaces and the document areas.
    internal static IEnumerable<IRelocatable> Relocatables(UIElement? node)
    {
        switch (node)
        {
            case DockingWindow window:
                yield return window;
                break;

            case DockingWindowContainer container:
                foreach (var hosted in container.Items)
                {
                    yield return hosted;
                }

                break;

            case Workspace workspace:
                yield return workspace;
                break;

            case DocumentHost host:
                yield return host;
                foreach (var relocatable in Relocatables(host.Child))
                {
                    yield return relocatable;
                }

                break;

            case SplitContainer split:
                foreach (var pane in split.GetPanes())
                {
                    foreach (var relocatable in Relocatables(pane))
                    {
                        yield return relocatable;
                    }
                }

                break;
        }
    }

    // Enumerates the containers of a layout tree, in layout order.
    internal static IEnumerable<DockingWindowContainer> Containers(UIElement? node)
    {
        switch (node)
        {
            case DockingWindowContainer container:
                yield return container;
                break;

            case DocumentHost host:
                foreach (var container in Containers(host.Child))
                {
                    yield return container;
                }

                break;

            case SplitContainer split:
                foreach (var pane in split.GetPanes())
                {
                    foreach (var container in Containers(pane))
                    {
                        yield return container;
                    }
                }

                break;
        }
    }

    // Enumerates the windows of a layout tree, in layout and tab order.
    internal static IEnumerable<DockingWindow> Windows(UIElement? node)
    {
        foreach (var container in Containers(node))
        {
            foreach (var window in container.Items)
            {
                yield return window;
            }
        }
    }

    // Enumerates the elements of a layout tree a drag can be dropped on: the panes, the workspaces,
    // and the document areas that hold nothing yet.
    //
    // Drop targets are found by walking the tree rather than kept in a registry the elements add
    // themselves to: panes move between the dock site and floating windows, and a moved element's
    // FrameworkElement.Unloaded arrives after it has already been reattached elsewhere, which would
    // silently drop it from such a registry.
    internal static IEnumerable<FrameworkElement> DropTargets(UIElement? node)
    {
        switch (node)
        {
            case DockingWindowContainer container:
                yield return container;
                break;

            case Workspace workspace:
                yield return workspace;
                break;

            case DocumentHost host:
                if (host.Child is null)
                {
                    yield return host;
                    break;
                }

                foreach (var target in DropTargets(host.Child))
                {
                    yield return target;
                }

                break;

            case SplitContainer split:
                foreach (var pane in split.GetPanes())
                {
                    foreach (var target in DropTargets(pane))
                    {
                        yield return target;
                    }
                }

                break;
        }
    }

    // Enumerates the document areas of a layout tree, in layout order.
    internal static IEnumerable<DocumentHost> DocumentHosts(UIElement? node)
    {
        switch (node)
        {
            case DocumentHost host:
                yield return host;
                break;

            case SplitContainer split:
                foreach (var pane in split.GetPanes())
                {
                    foreach (var host in DocumentHosts(pane))
                    {
                        yield return host;
                    }
                }

                break;
        }
    }
}
