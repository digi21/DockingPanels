namespace Digi21.WinUI.Docking;

/// <summary>
/// Where a node sits in the layout trees of a dock site, as found by
/// <see cref="LayoutTree.Locate(DockSite, Microsoft.UI.Xaml.UIElement)"/>.
/// </summary>
/// <param name="Surface">The dock site or floating window the node lives in.</param>
/// <param name="Host">
/// The element whose layout tree the node belongs to: the surface itself, or the document area
/// the node is inside.
/// </param>
/// <param name="Split">
/// The split container holding the node, or <see langword="null"/> when the node is the root of
/// its host's layout tree.
/// </param>
internal readonly record struct LayoutLocation(IDockSurface Surface, ILayoutHost Host, SplitContainer? Split);
