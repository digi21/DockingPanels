using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// The thin bar at a dock site edge that lists the auto-hidden tool windows of that edge.
/// Each auto-hide group is positioned along the strip near the offset its container had when
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
    private readonly List<AutoHideTabItem> tabs = [];
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

    // The tabs this strip is showing, in the order they were built. This is how an automation
    // client is told which of them, if any, has its panel out.
    internal IReadOnlyList<AutoHideTabItem> Tabs => tabs;

    // Replaces the auto-hide groups shown by this strip.
    internal void SetGroups(IEnumerable<AutoHideGroup> newGroups)
    {
        groups.Clear();
        groups.AddRange(newGroups);
        Rebuild();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new AutoHideTabStripAutomationPeer(this);

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
        tabs.Clear();

        var vertical = Edge is DockSide.Left or DockSide.Right;

        // The strip reserves a band at the dock site edge; each group's tabs are placed along it
        // near the offset the group's container had, so they stay aligned with where the panel used
        // to be, but never on top of another group's tabs.
        var host = new AutoHideStripPanel { IsVertical = vertical };

        foreach (var group in groups)
        {
            var panel = new StackPanel
            {
                Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal,
                Spacing = 2,
            };

            foreach (var window in group.Windows)
            {
                var tab = new AutoHideTabItem { Window = window, Edge = Edge };
                panel.Children.Add(tab);
                tabs.Add(tab);
            }

            AutoHideStripPanel.SetOffsetHint(panel, group.Offset);
            host.Children.Add(panel);
        }

        itemsHost.Children.Add(host);
    }
}
