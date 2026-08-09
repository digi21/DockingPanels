using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// The cluster of five dock guides (four sides plus the center attach-as-tab guide) shown
/// over the hovered drop target during a docking drag operation.
/// </summary>
public partial class DockGuidePanel : Control
{
    /// <summary>Identifies the <see cref="ShowCenter"/> dependency property.</summary>
    public static readonly DependencyProperty ShowCenterProperty = DependencyProperty.Register(
        nameof(ShowCenter),
        typeof(bool),
        typeof(DockGuidePanel),
        new PropertyMetadata(true, (d, _) => ((DockGuidePanel)d).UpdateCenterVisibility()));

    /// <summary>Identifies the <see cref="ShowSides"/> dependency property.</summary>
    public static readonly DependencyProperty ShowSidesProperty = DependencyProperty.Register(
        nameof(ShowSides),
        typeof(bool),
        typeof(DockGuidePanel),
        new PropertyMetadata(true, (d, _) => ((DockGuidePanel)d).UpdateSideVisibility()));

    /// <summary>Initializes a new instance of the <see cref="DockGuidePanel"/> class.</summary>
    public DockGuidePanel()
    {
        DefaultStyleKey = typeof(DockGuidePanel);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
    }

    /// <summary>
    /// Gets or sets a value indicating whether the center (attach-as-tab) guide is shown.
    /// It is hidden when the drop target cannot host tabs, such as the workspace.
    /// </summary>
    public bool ShowCenter
    {
        get => (bool)GetValue(ShowCenterProperty);
        set => SetValue(ShowCenterProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the four side guides are shown. They are hidden
    /// when the target can only be dropped into as a whole, such as an empty document area.
    /// </summary>
    public bool ShowSides
    {
        get => (bool)GetValue(ShowSidesProperty);
        set => SetValue(ShowSidesProperty, value);
    }

    internal DockGuide? CenterGuide { get; private set; }

    internal DockGuide? LeftGuide { get; private set; }

    internal DockGuide? TopGuide { get; private set; }

    internal DockGuide? RightGuide { get; private set; }

    internal DockGuide? BottomGuide { get; private set; }

    /// <summary>
    /// Gets a guide's bounds within the cluster, or <see cref="Rect.Empty"/> when that guide is
    /// hidden or the cluster has not been laid out yet. The center guide is the one with no side.
    /// </summary>
    /// <remarks>
    /// The drag code hit-tests the guides by geometry rather than by pointer routing, because the
    /// guides sit in a hit-test-invisible overlay. Measuring the elements instead of assuming the
    /// sizes of the default style is what keeps a retemplated cluster clickable where it is drawn.
    /// </remarks>
    internal Rect GuideBounds(DockSide? side)
    {
        var guide = side switch
        {
            DockSide.Left => LeftGuide,
            DockSide.Top => TopGuide,
            DockSide.Right => RightGuide,
            DockSide.Bottom => BottomGuide,
            _ => CenterGuide,
        };

        if (guide is not { Visibility: Visibility.Visible } || guide.ActualWidth <= 0 || guide.ActualHeight <= 0)
        {
            return Rect.Empty;
        }

        return guide
            .TransformToVisual(this)
            .TransformBounds(new Rect(0, 0, guide.ActualWidth, guide.ActualHeight));
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        CenterGuide = GetTemplateChild("PART_CenterGuide") as DockGuide;
        LeftGuide = GetTemplateChild("PART_LeftGuide") as DockGuide;
        TopGuide = GetTemplateChild("PART_TopGuide") as DockGuide;
        RightGuide = GetTemplateChild("PART_RightGuide") as DockGuide;
        BottomGuide = GetTemplateChild("PART_BottomGuide") as DockGuide;

        UpdateCenterVisibility();
        UpdateSideVisibility();
    }

    internal void SetHotGuide(DockSide? side, bool isCenterHot)
    {
        if (LeftGuide is not null)
        {
            LeftGuide.IsHot = side == DockSide.Left;
        }

        if (TopGuide is not null)
        {
            TopGuide.IsHot = side == DockSide.Top;
        }

        if (RightGuide is not null)
        {
            RightGuide.IsHot = side == DockSide.Right;
        }

        if (BottomGuide is not null)
        {
            BottomGuide.IsHot = side == DockSide.Bottom;
        }

        if (CenterGuide is not null)
        {
            CenterGuide.IsHot = isCenterHot;
        }
    }

    private void UpdateCenterVisibility()
    {
        if (CenterGuide is not null)
        {
            CenterGuide.Visibility = ShowCenter ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void UpdateSideVisibility()
    {
        var visibility = ShowSides ? Visibility.Visible : Visibility.Collapsed;

        foreach (var guide in new[] { LeftGuide, TopGuide, RightGuide, BottomGuide })
        {
            if (guide is not null)
            {
                guide.Visibility = visibility;
            }
        }
    }
}
