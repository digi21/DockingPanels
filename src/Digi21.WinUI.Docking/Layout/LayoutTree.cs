using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Reads a docking layout tree. The tree of a dock site, the tree of a floating window and the
/// tree inside a document host have the same shape (splits, containers, the workspace and the
/// document area), so the same walks serve all of them.
/// </summary>
internal static class LayoutTree
{
    /// <summary>Enumerates the containers of a layout tree, in layout order.</summary>
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

    /// <summary>Enumerates the windows of a layout tree, in layout and tab order.</summary>
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

    /// <summary>
    /// Enumerates the elements of a layout tree a drag can be dropped on: the panes, the
    /// workspaces, and the document areas that hold nothing yet.
    /// </summary>
    /// <remarks>
    /// Drop targets are found by walking the tree rather than kept in a registry the elements
    /// add themselves to: panes move between the dock site and floating windows, and a moved
    /// element's <see cref="FrameworkElement.Unloaded"/> arrives after it has already been
    /// reattached elsewhere, which would silently drop it from such a registry.
    /// </remarks>
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

    /// <summary>Enumerates the document areas of a layout tree, in layout order.</summary>
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
