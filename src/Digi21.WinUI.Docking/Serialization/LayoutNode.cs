using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking.Serialization;

/// <summary>
/// In-memory model of a serialized docking layout. The serializer converts between this model
/// and both the XML format and the live control tree, keeping the XML logic free of UI types
/// so it can be unit tested without a XAML runtime.
/// </summary>
internal abstract class LayoutNode
{
    /// <summary>Gets or sets the node's proportional share of space inside its parent split.</summary>
    internal double RelativeSize { get; set; } = 1.0;
}

/// <summary>A split container holding two or more child nodes.</summary>
internal sealed class SplitLayoutNode : LayoutNode
{
    /// <summary>Gets or sets the split orientation.</summary>
    internal Orientation Orientation { get; set; }

    /// <summary>Gets the child nodes in layout order.</summary>
    internal List<LayoutNode> Children { get; } = [];
}

/// <summary>A tool window entry inside a serialized container.</summary>
internal readonly record struct LayoutWindowEntry(string Id, string State);

/// <summary>A tool window container with its tabs.</summary>
internal sealed class ContainerLayoutNode : LayoutNode
{
    /// <summary>Gets the windows hosted by the container, in tab order.</summary>
    internal List<LayoutWindowEntry> Windows { get; } = [];

    /// <summary>Gets or sets the id of the selected window, if any.</summary>
    internal string? SelectedId { get; set; }
}

/// <summary>The central workspace node.</summary>
internal sealed class WorkspaceLayoutNode : LayoutNode
{
}

/// <summary>A serialized auto-hide group collapsed to a dock site edge.</summary>
internal sealed class AutoHideGroupNode
{
    /// <summary>Gets or sets the edge the group is collapsed to.</summary>
    internal DockSide Edge { get; set; }

    /// <summary>Gets or sets the flyout size in pixels.</summary>
    internal double Size { get; set; } = 300;

    /// <summary>Gets or sets the position of the tab group along the edge, in pixels.</summary>
    internal double Offset { get; set; }

    /// <summary>
    /// Gets or sets a stable reference to the pane the group re-docks next to when pinned
    /// back ("Workspace:n" for the n-th workspace in document order, "Window:id" for the
    /// container hosting that window), or <see langword="null"/> when the group falls back
    /// to docking at its edge.
    /// </summary>
    internal string? RestoreSibling { get; set; }

    /// <summary>Gets or sets the side of the restore sibling the container sat on.</summary>
    internal DockSide RestoreSide { get; set; }

    /// <summary>Gets or sets the container's relative size at unpin time.</summary>
    internal double RestoreRelativeSize { get; set; } = 1.0;

    /// <summary>Gets the windows in the group, in tab order.</summary>
    internal List<LayoutWindowEntry> Windows { get; } = [];
}

/// <summary>The full serialized layout: the docked tree plus the auto-hide groups.</summary>
internal sealed class LayoutDocument
{
    /// <summary>Gets or sets the root of the docked layout tree, if any.</summary>
    internal LayoutNode? Root { get; set; }

    /// <summary>Gets the auto-hide groups.</summary>
    internal List<AutoHideGroupNode> AutoHideGroups { get; } = [];
}
