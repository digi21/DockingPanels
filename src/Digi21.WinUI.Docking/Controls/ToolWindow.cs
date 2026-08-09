namespace Digi21.WinUI.Docking;

/// <summary>
/// A dockable tool window. Tool windows are hosted in a <see cref="ToolWindowContainer"/>,
/// dock to the sides of the <see cref="Workspace"/> or of each other, and become tabs when
/// several share the same container.
/// </summary>
public partial class ToolWindow : DockingWindow
{
    /// <summary>Initializes a new instance of the <see cref="ToolWindow"/> class.</summary>
    public ToolWindow()
    {
        DefaultStyleKey = typeof(ToolWindow);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");

        // The templates below reference the library's own brush and metric keys; this is what
        // puts them within reach of the application's resources.
        DockingThemeResources.Ensure();
    }
}
