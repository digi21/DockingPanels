using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Reads a docking layout tree. The tree of a dock site and the tree of a floating window have
/// the same shape (splits, containers and the workspace), so the same walks serve both.
/// </summary>
internal static class LayoutTree
{
    /// <summary>Enumerates the tool window containers of a layout tree, in layout order.</summary>
    internal static IEnumerable<ToolWindowContainer> Containers(UIElement? node)
    {
        switch (node)
        {
            case ToolWindowContainer container:
                yield return container;
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

    /// <summary>Enumerates the tool windows of a layout tree, in layout and tab order.</summary>
    internal static IEnumerable<ToolWindow> Windows(UIElement? node)
    {
        foreach (var container in Containers(node))
        {
            foreach (var window in container.Items)
            {
                yield return window;
            }
        }
    }
}
