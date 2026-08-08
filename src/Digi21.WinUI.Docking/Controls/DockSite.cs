using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

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

    /// <summary>Identifies the <c>DockSite.RelativeSize</c> attached dependency property.</summary>
    public static readonly DependencyProperty RelativeSizeProperty = DependencyProperty.RegisterAttached(
        "RelativeSize",
        typeof(double),
        typeof(DockSite),
        new PropertyMetadata(1.0, OnRelativeSizeChanged));

    /// <summary>
    /// Gets the proportional share of space the element receives inside a <see cref="SplitContainer"/>.
    /// </summary>
    /// <param name="element">The element to read the value from.</param>
    public static double GetRelativeSize(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (double)element.GetValue(RelativeSizeProperty);
    }

    /// <summary>
    /// Sets the proportional share of space the element receives inside a <see cref="SplitContainer"/>.
    /// </summary>
    /// <param name="element">The element to set the value on.</param>
    /// <param name="value">The relative size. Values are proportions, not pixels; siblings share space by ratio.</param>
    public static void SetRelativeSize(UIElement element, double value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(RelativeSizeProperty, value);
    }

    private static void OnRelativeSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is UIElement element && VisualTreeHelper.GetParent(element) is SplitContainer parent)
        {
            parent.InvalidateMeasure();
            parent.InvalidateArrange();
        }
    }

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
