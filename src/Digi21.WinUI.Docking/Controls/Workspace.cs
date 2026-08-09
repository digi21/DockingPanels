using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking;

/// <summary>
/// The central content area of a <see cref="DockSite"/> for applications that do not use
/// documents. Tool windows dock around it, but the workspace itself always remains part of the
/// layout. Applications with documents use a <see cref="DocumentHost"/> instead, or as well.
/// </summary>
public partial class Workspace : ContentControl, IRelocatable
{
    /// <summary>
    /// Raised after a docking operation has moved this workspace to a different place in the XAML
    /// tree, once the tree has settled. Content with a life cycle of its own — a
    /// <c>SwapChainPanel</c>, a <c>WebView2</c>, a render loop — should hang off this event
    /// instead of off <see cref="FrameworkElement.Loaded"/> and
    /// <see cref="FrameworkElement.Unloaded"/>, which WinUI raises in that order for an element
    /// that never left the tree.
    /// </summary>
    public event EventHandler? Relocated;

    /// <summary>Initializes a new instance of the <see cref="Workspace"/> class.</summary>
    public Workspace()
    {
        DefaultStyleKey = typeof(Workspace);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");

        // The templates below reference the library's own brush and metric keys; this is what
        // puts them within reach of the application's resources.
        DockingThemeResources.Ensure();
    }

    void IRelocatable.RaiseRelocated() => Relocated?.Invoke(this, EventArgs.Empty);
}
