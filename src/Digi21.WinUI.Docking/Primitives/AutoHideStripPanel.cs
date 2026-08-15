using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// Hosts the tab groups of an <see cref="AutoHideTabStrip"/>, each one at the offset along the
/// edge that <see cref="AutoHideStripLayout"/> gives it.
/// </summary>
internal sealed partial class AutoHideStripPanel : Panel
{
    // Where along the strip a group would like to sit; see AutoHideStripLayout.
    internal static readonly DependencyProperty OffsetHintProperty = DependencyProperty.RegisterAttached(
        "OffsetHint",
        typeof(double),
        typeof(AutoHideStripPanel),
        new PropertyMetadata(0d));

    private double[] positions = [];

    /// <summary>Gets or sets a value indicating whether the strip runs down a side edge.</summary>
    internal bool IsVertical { get; set; }

    internal static void SetOffsetHint(DependencyObject element, double value) => element.SetValue(OffsetHintProperty, value);

    internal static double GetOffsetHint(DependencyObject element) => (double)element.GetValue(OffsetHintProperty);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var along = IsVertical ? availableSize.Height : availableSize.Width;
        var across = IsVertical ? availableSize.Width : availableSize.Height;

        var groups = new List<(double Hint, double Length)>(Children.Count);
        var thickness = 0.0;

        foreach (var child in Children)
        {
            child.Measure(IsVertical
                ? new Size(across, double.PositiveInfinity)
                : new Size(double.PositiveInfinity, across));

            var desired = child.DesiredSize;
            groups.Add((GetOffsetHint(child), IsVertical ? desired.Height : desired.Width));
            thickness = Math.Max(thickness, IsVertical ? desired.Width : desired.Height);
        }

        positions = AutoHideStripLayout.Place(groups, along);

        var extent = 0.0;
        for (var i = 0; i < groups.Count; i++)
        {
            extent = Math.Max(extent, positions[i] + Math.Max(groups[i].Length, 0));
        }

        // The strip is a band along one edge: it asks for the thickness of its widest tab, and for
        // no more room along the edge than it was offered.
        if (double.IsFinite(along))
        {
            extent = Math.Min(extent, along);
        }

        return IsVertical ? new Size(thickness, extent) : new Size(extent, thickness);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var index = 0;

        foreach (var child in Children)
        {
            var position = index < positions.Length ? positions[index] : 0;
            var desired = child.DesiredSize;

            // Across the strip every group gets the full band, so tabs of different groups line up
            // instead of each one being as wide as its own longest title.
            child.Arrange(IsVertical
                ? new Rect(0, position, finalSize.Width, desired.Height)
                : new Rect(position, 0, desired.Width, finalSize.Height));

            index++;
        }

        return finalSize;
    }
}
