using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

/// <summary>
/// A dockable tool window. Tool windows are hosted in a <see cref="ToolWindowContainer"/>,
/// dock to the sides of the <see cref="Workspace"/> or of each other, and become tabs when
/// several share the same container.
/// </summary>
public partial class ToolWindow : DockingWindow
{
    /// <summary>Identifies the <see cref="PreferredDockSide"/> dependency property.</summary>
    public static readonly DependencyProperty PreferredDockSideProperty = DependencyProperty.Register(
        nameof(PreferredDockSide),
        typeof(DockSide),
        typeof(ToolWindow),
        new PropertyMetadata(DockSide.Left));

    /// <summary>Initializes a new instance of the <see cref="ToolWindow"/> class.</summary>
    public ToolWindow()
    {
        DefaultStyleKey = typeof(ToolWindow);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");

        // The templates below reference the library's own brush and metric keys; this is what
        // puts them within reach of the application's resources.
        DockingThemeResources.Ensure();
    }

    /// <summary>
    /// Gets or sets the dock site edge this window belongs at when the library has to place it
    /// with nothing else to go on. The default is <see cref="DockSide.Left"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// That happens in one place: loading a layout that does not mention a window which stays open
    /// anyway — either because <see cref="Serialization.DockSiteLayoutSerializer.UnresolvedWindowBehavior"/>
    /// is <see cref="Serialization.UnresolvedWindowBehavior.DockLeft"/>, or because the window is
    /// declared with <c>CanClose="False"</c> and closing it would leave the user no way back. The
    /// window is docked as a new pane at this edge, which is the application's business and not the
    /// layout file's: a panel the application never puts on the left should not appear there the
    /// first time an older saved layout is loaded.
    /// </para>
    /// <para>
    /// It is a preference and not a promise. Everything else that places a window — a drag, a
    /// docking call, pinning a group back — has somewhere better to go and ignores this. For
    /// placement that a single edge cannot express, such as rejoining the tab group the window
    /// belongs with, handle
    /// <see cref="Serialization.DockSiteLayoutSerializer.UnresolvedWindowDocking"/> instead.
    /// </para>
    /// </remarks>
    public DockSide PreferredDockSide
    {
        get => (DockSide)GetValue(PreferredDockSideProperty);
        set => SetValue(PreferredDockSideProperty, value);
    }
}
