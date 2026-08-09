namespace Digi21.WinUI.Docking;

// Where a node sits in the layout trees of a dock site, as found by LayoutTree.Locate(DockSite,
// Microsoft.UI.Xaml.UIElement).
//
// Surface: The dock site or floating window the node lives in.
//
// Host: The element whose layout tree the node belongs to: the surface itself, or the document area
// the node is inside.
//
// Split: The split container holding the node, or null when the node is the root of its host's
// layout tree.
internal readonly record struct LayoutLocation(IDockSurface Surface, ILayoutHost Host, SplitContainer? Split);
