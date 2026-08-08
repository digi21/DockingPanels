using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Digi21.WinUI.Docking;

/// <summary>
/// The root control of a docking layout. Hosts a tree of docking containers
/// (such as <c>SplitContainer</c> and <c>ToolWindowContainer</c>) and the central workspace.
/// </summary>
[ContentProperty(Name = nameof(Child))]
public partial class DockSite : Control
{
    /// <summary>Identifies the <see cref="Child"/> dependency property.</summary>
    public static readonly DependencyProperty ChildProperty = DependencyProperty.Register(
        nameof(Child),
        typeof(UIElement),
        typeof(DockSite),
        new PropertyMetadata(null));

    /// <summary>Initializes a new instance of the <see cref="DockSite"/> class.</summary>
    public DockSite()
    {
        DefaultStyleKey = typeof(DockSite);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
    }

    /// <summary>
    /// Gets or sets the root element of the docking layout tree.
    /// </summary>
    public UIElement? Child
    {
        get => (UIElement?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }
}
