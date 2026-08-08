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
