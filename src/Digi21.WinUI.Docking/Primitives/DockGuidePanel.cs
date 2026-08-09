using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// The cluster of five dock guides (four sides plus the center attach-as-tab guide) shown
/// over the hovered drop target during a docking drag operation.
/// </summary>
public partial class DockGuidePanel : Control
{
    /// <summary>The width and height of a single guide, matching the default style.</summary>
    internal const double GuideSize = 40.0;

    /// <summary>The width and height of the whole cluster, matching the default style.</summary>
    internal const double ClusterSize = 128.0;

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

    /// <summary>Gets the guide's position (top-left corner) within the cluster.</summary>
    internal static Windows.Foundation.Point GuideOffset(DockSide? side)
    {
        const double edge = (ClusterSize - GuideSize) / 2;
        return side switch
        {
            DockSide.Left => new(0, edge),
            DockSide.Top => new(edge, 0),
            DockSide.Right => new(ClusterSize - GuideSize, edge),
            DockSide.Bottom => new(edge, ClusterSize - GuideSize),
            _ => new(edge, edge),
        };
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
