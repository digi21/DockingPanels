using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// The thin bar at a dock site edge that lists the auto-hidden tool windows of that edge.
/// Each auto-hide group is positioned along the strip at the offset its container had when
/// it was unpinned, so the tabs appear aligned with where the panel used to be.
/// </summary>
public partial class AutoHideTabStrip : Control
{
    /// <summary>Identifies the <see cref="Edge"/> dependency property.</summary>
    public static readonly DependencyProperty EdgeProperty = DependencyProperty.Register(
        nameof(Edge),
        typeof(DockSide),
        typeof(AutoHideTabStrip),
        new PropertyMetadata(DockSide.Left, (d, _) => ((AutoHideTabStrip)d).Rebuild()));

    private readonly List<AutoHideGroup> groups = [];
    private Grid? itemsHost;

    /// <summary>Initializes a new instance of the <see cref="AutoHideTabStrip"/> class.</summary>
    public AutoHideTabStrip()
    {
        DefaultStyleKey = typeof(AutoHideTabStrip);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
    }

    /// <summary>Gets or sets the dock site edge this strip lives on.</summary>
    public DockSide Edge
    {
        get => (DockSide)GetValue(EdgeProperty);
        set => SetValue(EdgeProperty, value);
    }

    /// <summary>Replaces the auto-hide groups shown by this strip.</summary>
    internal void SetGroups(IEnumerable<AutoHideGroup> newGroups)
    {
        groups.Clear();
        groups.AddRange(newGroups);
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        itemsHost = GetTemplateChild("PART_Items") as Grid;
        Rebuild();
    }

    private void Rebuild()
    {
        if (itemsHost is null)
        {
            return;
        }

        itemsHost.Children.Clear();

        var vertical = Edge is DockSide.Left or DockSide.Right;

        foreach (var group in groups)
        {
            // The strip itself is transparent and overlays the content edge; each group panel
            // paints its own background, so docked panels outside the group's range (e.g. a
            // full-height left panel) keep reaching the edge without an empty band under them.
            var panel = new StackPanel
            {
                Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal,
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = vertical ? new Thickness(0, group.Offset, 0, 0) : new Thickness(group.Offset, 0, 0, 0),
                Background = Application.Current.Resources.TryGetValue("LayerFillColorDefaultBrush", out var brush)
                    ? brush as Microsoft.UI.Xaml.Media.Brush
                    : null,
            };

            foreach (var window in group.Windows)
            {
                panel.Children.Add(new AutoHideTabItem { Window = window, Edge = Edge });
            }

            itemsHost.Children.Add(panel);
        }
    }
}
