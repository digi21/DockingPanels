using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking;

/// <summary>
/// The central content area of a <see cref="DockSite"/>. Tool windows dock around it,
/// but the workspace itself always remains part of the layout.
/// </summary>
public partial class Workspace : ContentControl
{
    private DockSite? registeredSite;

    /// <summary>Initializes a new instance of the <see cref="Workspace"/> class.</summary>
    public Workspace()
    {
        DefaultStyleKey = typeof(Workspace);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");

        Loaded += (_, _) =>
        {
            registeredSite = this.FindAncestor<DockSite>();
            registeredSite?.RegisterDropTarget(this);
        };
        Unloaded += (_, _) =>
        {
            registeredSite?.UnregisterDropTarget(this);
            registeredSite = null;
        };
    }
}
