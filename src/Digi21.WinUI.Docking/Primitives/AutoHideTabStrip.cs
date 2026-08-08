using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// The thin bar at a dock site edge that lists the auto-hidden tool windows of that edge.
/// </summary>
public partial class AutoHideTabStrip : Control
{
    /// <summary>Identifies the <see cref="Edge"/> dependency property.</summary>
    public static readonly DependencyProperty EdgeProperty = DependencyProperty.Register(
        nameof(Edge),
        typeof(DockSide),
        typeof(AutoHideTabStrip),
        new PropertyMetadata(DockSide.Left, (d, _) => ((AutoHideTabStrip)d).Rebuild()));

    private readonly List<ToolWindow> windows = [];
    private StackPanel? itemsHost;

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

    /// <summary>Replaces the windows shown by this strip.</summary>
    internal void SetWindows(IEnumerable<ToolWindow> newWindows)
    {
        windows.Clear();
        windows.AddRange(newWindows);
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        itemsHost = GetTemplateChild("PART_Items") as StackPanel;
        Rebuild();
    }

    private void Rebuild()
    {
        if (itemsHost is null)
        {
            return;
        }

        itemsHost.Orientation = Edge is DockSide.Left or DockSide.Right ? Orientation.Vertical : Orientation.Horizontal;
        itemsHost.Children.Clear();

        foreach (var window in windows)
        {
            itemsHost.Children.Add(new AutoHideTabItem { Window = window, Edge = Edge });
        }
    }
}
