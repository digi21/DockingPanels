using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

// An element that owns the root of a docking layout tree: the DockSite, every floating window, and
// every DocumentHost.
//
// Layout mutations walk up the visual tree until they find either a SplitContainer or one of these
// hosts, which is what lets the same code split, collapse and replace panes in the dock site,
// inside a floating window, and inside the document area, without knowing which of the three it is
// working in.
internal interface ILayoutHost
{
    // Gets or sets the root of the layout tree hosted by this element.
    UIElement? LayoutChild { get; set; }
}
