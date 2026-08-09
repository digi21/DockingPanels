using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Hosts one or more <see cref="ToolWindow"/> instances. A single window shows a title bar;
/// multiple windows additionally become tabs at the bottom of the container. All hosted windows
/// stay loaded and switching tabs only toggles visibility, so control state is preserved.
/// </summary>
public partial class ToolWindowContainer : DockingWindowContainer
{
    /// <summary>Initializes a new instance of the <see cref="ToolWindowContainer"/> class.</summary>
    public ToolWindowContainer()
    {
        DefaultStyleKey = typeof(ToolWindowContainer);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");

        // The templates below reference the library's own brush and metric keys; this is what
        // puts them within reach of the application's resources.
        DockingThemeResources.Ensure();
    }

    /// <inheritdoc />
    protected override Control CreateTab(DockingWindow window) => new ToolWindowTabItem { Window = window };

    /// <summary>
    /// Tool window tabs only appear once the container holds more than one window: with a single
    /// one its title bar already names it.
    /// </summary>
    /// <param name="count">The number of windows currently hosted.</param>
    protected override bool ShowTabs(int count) => count > 1;

    /// <summary>The title bar shown above the tabs is what a floating window uses as its caption.</summary>
    internal override bool ProvidesWindowCaption => true;
}
