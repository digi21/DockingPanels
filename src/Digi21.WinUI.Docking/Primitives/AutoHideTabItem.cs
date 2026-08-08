using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// A tab in an <see cref="AutoHideTabStrip"/> representing one auto-hidden tool window.
/// Clicking it opens the window's flyout. On vertical edges the whole tab is rotated 90°
/// (WinUI 3 has no LayoutTransform, so the rotation is done in measure/arrange).
/// </summary>
public partial class AutoHideTabItem : Control
{
    /// <summary>Identifies the <see cref="Window"/> dependency property.</summary>
    public static readonly DependencyProperty WindowProperty = DependencyProperty.Register(
        nameof(Window),
        typeof(ToolWindow),
        typeof(AutoHideTabItem),
        new PropertyMetadata(null, (d, _) => ((AutoHideTabItem)d).OnWindowChanged()));

    /// <summary>Identifies the <see cref="Edge"/> dependency property.</summary>
    public static readonly DependencyProperty EdgeProperty = DependencyProperty.Register(
        nameof(Edge),
        typeof(DockSide),
        typeof(AutoHideTabItem),
        new PropertyMetadata(DockSide.Left, (d, _) => ((AutoHideTabItem)d).InvalidateMeasure()));

    private ToolWindow? observed;
    private long titleToken = -1;
    private TextBlock? titleText;

    /// <summary>Initializes a new instance of the <see cref="AutoHideTabItem"/> class.</summary>
    public AutoHideTabItem()
    {
        DefaultStyleKey = typeof(AutoHideTabItem);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
    }

    /// <summary>Gets or sets the auto-hidden window this tab represents.</summary>
    public ToolWindow? Window
    {
        get => (ToolWindow?)GetValue(WindowProperty);
        set => SetValue(WindowProperty, value);
    }

    /// <summary>Gets or sets the dock site edge the tab strip lives on.</summary>
    public DockSide Edge
    {
        get => (DockSide)GetValue(EdgeProperty);
        set => SetValue(EdgeProperty, value);
    }

    private bool IsVertical => Edge is DockSide.Left or DockSide.Right;

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        titleText = GetTemplateChild("PART_Title") as TextBlock;
        Update();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Window is { } window)
        {
            this.FindAncestor<DockSite>()?.ShowAutoHideFlyout(window);
        }
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (GetTemplateRoot() is not { } child)
        {
            return new Size(0, 0);
        }

        if (!IsVertical)
        {
            child.Measure(availableSize);
            return child.DesiredSize;
        }

        child.Measure(new Size(availableSize.Height, availableSize.Width));
        return new Size(child.DesiredSize.Height, child.DesiredSize.Width);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (GetTemplateRoot() is not { } child)
        {
            return finalSize;
        }

        if (!IsVertical)
        {
            child.RenderTransform = null;
            child.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            return finalSize;
        }

        // Arrange unrotated, then rotate 90° clockwise and shift right into place.
        child.Arrange(new Rect(0, 0, finalSize.Height, finalSize.Width));
        child.RenderTransform = new TransformGroup
        {
            Children =
            {
                new RotateTransform { Angle = 90 },
                new TranslateTransform { X = finalSize.Width },
            },
        };

        return finalSize;
    }

    private UIElement? GetTemplateRoot()
    {
        return VisualTreeHelper.GetChildrenCount(this) > 0 ? VisualTreeHelper.GetChild(this, 0) as UIElement : null;
    }

    private void OnWindowChanged()
    {
        if (observed is not null)
        {
            observed.UnregisterPropertyChangedCallback(DockingWindow.TitleProperty, titleToken);
        }

        observed = Window;

        if (observed is not null)
        {
            titleToken = observed.RegisterPropertyChangedCallback(DockingWindow.TitleProperty, (_, _) => Update());
        }

        Update();
    }

    private void Update()
    {
        if (titleText is not null)
        {
            titleText.Text = observed?.Title ?? string.Empty;
        }
    }
}
