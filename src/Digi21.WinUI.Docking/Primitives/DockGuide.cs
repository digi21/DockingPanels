using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// A single drop guide shown during a docking drag operation. Guides light up ("hot")
/// while the pointer is over them and determine the dock action performed on drop.
/// </summary>
public partial class DockGuide : Control
{
    /// <summary>Identifies the <see cref="Side"/> dependency property.</summary>
    public static readonly DependencyProperty SideProperty = DependencyProperty.Register(
        nameof(Side),
        typeof(DockSide),
        typeof(DockGuide),
        new PropertyMetadata(DockSide.Left, (d, _) => ((DockGuide)d).UpdateIcon()));

    /// <summary>Identifies the <see cref="IsCenter"/> dependency property.</summary>
    public static readonly DependencyProperty IsCenterProperty = DependencyProperty.Register(
        nameof(IsCenter),
        typeof(bool),
        typeof(DockGuide),
        new PropertyMetadata(false, (d, _) => ((DockGuide)d).UpdateIcon()));

    /// <summary>Identifies the <see cref="IsHot"/> dependency property.</summary>
    public static readonly DependencyProperty IsHotProperty = DependencyProperty.Register(
        nameof(IsHot),
        typeof(bool),
        typeof(DockGuide),
        new PropertyMetadata(false, (d, _) => ((DockGuide)d).UpdateState()));

    /// <summary>Initializes a new instance of the <see cref="DockGuide"/> class.</summary>
    public DockGuide()
    {
        DefaultStyleKey = typeof(DockGuide);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
    }

    /// <summary>Gets or sets the dock side this guide represents. Ignored when <see cref="IsCenter"/> is set.</summary>
    public DockSide Side
    {
        get => (DockSide)GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether this is the center (attach-as-tab) guide.</summary>
    public bool IsCenter
    {
        get => (bool)GetValue(IsCenterProperty);
        set => SetValue(IsCenterProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the pointer is currently over this guide.</summary>
    public bool IsHot
    {
        get => (bool)GetValue(IsHotProperty);
        set => SetValue(IsHotProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateIcon();
        UpdateState();
    }

    private void UpdateIcon()
    {
        if (GetTemplateChild("PART_Icon") is FontIcon icon)
        {
            icon.Glyph = IsCenter
                ? ""
                : Side switch
                {
                    DockSide.Left => "",
                    DockSide.Top => "",
                    DockSide.Right => "",
                    _ => "",
                };
        }
    }

    private void UpdateState()
    {
        VisualStateManager.GoToState(this, IsHot ? "Hot" : "Normal", true);
    }
}
