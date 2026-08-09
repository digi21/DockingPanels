using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

/// <summary>
/// A surface that hosts a docking layout tree and can show dock guides over it: the
/// <see cref="DockSite"/> itself and every floating window.
/// </summary>
/// <remarks>
/// Making both interchangeable is what lets windows be docked <em>inside</em> a floating window:
/// the drag gesture, the guides and every layout mutation work against a surface instead of
/// against the dock site, so a floating window behaves as a small dock site of its own. The
/// surface a layout element belongs to is found by walking up the visual tree
/// (<see cref="VisualTreeExtensions.FindSurface"/>), so operations never need to be told which
/// window they are running in.
/// </remarks>
internal interface IDockSurface
{
    /// <summary>Gets the dock site that owns this surface (the site itself, for a dock site).</summary>
    DockSite Site { get; }

    /// <summary>Gets the element drag coordinates and guide positions are measured against.</summary>
    FrameworkElement Root { get; }

    /// <summary>Gets a value indicating whether this surface is a floating window.</summary>
    bool IsFloating { get; }

    /// <summary>Gets or sets the root of the layout tree hosted by this surface.</summary>
    UIElement? LayoutChild { get; set; }

    /// <summary>Gets the guide overlay, or <see langword="null"/> until the template is applied.</summary>
    DockGuideOverlay? Overlay { get; }

    /// <summary>Gets the elements (containers and workspaces) that can receive dropped windows.</summary>
    IReadOnlyCollection<FrameworkElement> DropTargets { get; }

    /// <summary>Adds an element to the set of drop targets considered during drag operations.</summary>
    void RegisterDropTarget(FrameworkElement element);

    /// <summary>Removes an element from the set of drop targets.</summary>
    void UnregisterDropTarget(FrameworkElement element);

    /// <summary>Called after the layout tree of this surface has been mutated.</summary>
    void OnLayoutMutated();
}
