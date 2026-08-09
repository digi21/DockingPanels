using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

// A surface that hosts a docking layout tree and can show dock guides over it: the DockSite itself
// and every floating window.
//
// Making both interchangeable is what lets windows be docked inside a floating window: the drag
// gesture, the guides and every layout mutation work against a surface instead of against the dock
// site, so a floating window behaves as a small dock site of its own. The surface a layout element
// belongs to is found by walking up the visual tree (VisualTreeExtensions.FindSurface), so
// operations never need to be told which window they are running in.
internal interface IDockSurface : ILayoutHost
{
    // Gets the dock site that owns this surface (the site itself, for a dock site).
    DockSite Site { get; }

    // Gets the element drag coordinates and guide positions are measured against.
    FrameworkElement Root { get; }

    // Gets a value indicating whether this surface is a floating window.
    bool IsFloating { get; }

    // Gets the guide overlay, or null until the template is applied.
    DockGuideOverlay? Overlay { get; }

    // Called after the layout tree of this surface has been mutated.
    void OnLayoutMutated();
}
