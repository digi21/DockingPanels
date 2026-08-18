using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking.Serialization;

// In-memory model of a serialized docking layout. The serializer converts between this model and
// both the XML format and the live control tree, keeping the XML logic free of UI types so it can
// be unit tested without a XAML runtime.
internal abstract class LayoutNode
{
    // Gets or sets the node's proportional share of space inside its parent split.
    internal double RelativeSize { get; set; } = 1.0;
}

// A split container holding two or more child nodes.
internal sealed class SplitLayoutNode : LayoutNode
{
    // Gets or sets the split orientation.
    internal Orientation Orientation { get; set; }

    // Gets the child nodes in layout order.
    internal List<LayoutNode> Children { get; } = [];
}

// A window entry inside a serialized container.
//
// CanAutoHide is null when the layout says nothing about it, which is what every file written
// before the attribute existed looks like: the window keeps whatever it was declared with.
//
// IsPinned only ever applies to a document, whose tab is pinned to the head of its group; a file
// written before the attribute existed simply has none pinned.
internal readonly record struct LayoutWindowEntry(
    string Id,
    string State,
    bool? CanAutoHide = null,
    bool IsPinned = false);

// A tool window container with its tabs.
internal sealed class ContainerLayoutNode : LayoutNode
{
    // Gets the windows hosted by the container, in tab order.
    internal List<LayoutWindowEntry> Windows { get; } = [];

    // Gets or sets the id of the selected window, if any.
    internal string? SelectedId { get; set; }
}

// The central workspace node.
internal sealed class WorkspaceLayoutNode : LayoutNode
{
}

// The document area, with the tree of tab groups it holds.
internal sealed class DocumentHostLayoutNode : LayoutNode
{
    // Gets or sets the root of the tree inside the document area: a single tab group, a split of
    // several, or null when no document is open.
    internal LayoutNode? Root { get; set; }
}

// A document tab group with its tabs.
internal sealed class DocumentContainerLayoutNode : LayoutNode
{
    // Gets the documents hosted by the group, in tab order.
    internal List<LayoutWindowEntry> Windows { get; } = [];

    // Gets or sets the id of the selected document, if any.
    internal string? SelectedId { get; set; }
}

// A serialized auto-hide group collapsed to a dock site edge.
internal sealed class AutoHideGroupNode
{
    // Gets or sets the edge the group is collapsed to.
    internal DockSide Edge { get; set; }

    // Gets or sets the flyout size in pixels.
    internal double Size { get; set; } = 300;

    // Gets or sets the position of the tab group along the edge, in pixels.
    internal double Offset { get; set; }

    // Gets or sets a stable reference to the pane the group re-docks next to when pinned back
    // ("Workspace:n" for the n-th workspace in document order, "Window:id" for the container
    // hosting that window), or null when the group falls back to docking at its edge.
    internal string? RestoreSibling { get; set; }

    // Gets or sets the side of the restore sibling the container sat on.
    internal DockSide RestoreSide { get; set; }

    // Gets or sets the container's relative size at unpin time.
    internal double RestoreRelativeSize { get; set; } = 1.0;

    // Gets the windows in the group, in tab order.
    internal List<LayoutWindowEntry> Windows { get; } = [];
}

// A serialized floating window with the layout tree it hosts.
internal sealed class FloatingWindowNode
{
    // Gets or sets the root of the window's layout tree: a single container for a floating window
    // with one pane, or a split for one that has windows docked inside it.
    internal LayoutNode? Root { get; set; }

    // Gets or sets the window bounds in screen pixels.
    internal int X { get; set; }

    internal int Y { get; set; }

    internal int Width { get; set; } = 380;

    internal int Height { get; set; } = 300;

    // Gets or sets a "Window:id" reference to the container the windows were floated from, so
    // docking them back rejoins that tab group when it still exists.
    internal string? RestoreContainer { get; set; }

    // Gets or sets a stable reference to the pane the windows re-dock next to when the original
    // container is gone ("Workspace:n" or "Window:id"), or null when they fall back to a dock site
    // edge.
    internal string? RestoreSibling { get; set; }

    // Gets or sets the side of the restore sibling the container sat on.
    internal DockSide RestoreSide { get; set; }

    // Gets or sets the container's relative size when it was floated.
    internal double RestoreRelativeSize { get; set; } = 1.0;
}

// The full serialized layout: the docked tree, the auto-hide groups and the floating windows.
internal sealed class LayoutDocument
{
    // Gets or sets the root of the docked layout tree, if any.
    internal LayoutNode? Root { get; set; }

    // Gets the auto-hide groups.
    internal List<AutoHideGroupNode> AutoHideGroups { get; } = [];

    // Gets the floating windows.
    internal List<FloatingWindowNode> FloatingWindows { get; } = [];
}
