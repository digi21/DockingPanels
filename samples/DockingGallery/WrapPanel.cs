using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace DockingGallery;

// Lays its children out left to right and starts a new row when the next one does not fit, so a toolbar with many buttons
// rolls over onto a second line instead of running past the edge of the window. Each row is as high as its tallest child.
public sealed partial class WrapPanel : Panel
{
    public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
        nameof(HorizontalSpacing), typeof(double), typeof(WrapPanel), new PropertyMetadata(0.0, (d, _) => ((WrapPanel)d).InvalidateMeasure()));

    public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
        nameof(VerticalSpacing), typeof(double), typeof(WrapPanel), new PropertyMetadata(0.0, (d, _) => ((WrapPanel)d).InvalidateMeasure()));

    // The space between two children on a row.
    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    // The space between two rows.
    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var rows = Rows(availableSize.Width, measure: true);
        var width = rows.Count == 0 ? 0 : rows.Max(row => row.Width);
        var height = rows.Sum(row => row.Height) + (Math.Max(0, rows.Count - 1) * VerticalSpacing);
        return new Size(double.IsInfinity(availableSize.Width) ? width : Math.Min(width, availableSize.Width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var row in Rows(finalSize.Width, measure: false))
        {
            var x = 0.0;
            foreach (var child in row.Children)
            {
                child.Arrange(new Rect(x, y, child.DesiredSize.Width, row.Height));
                x += child.DesiredSize.Width + HorizontalSpacing;
            }

            y += row.Height + VerticalSpacing;
        }

        return finalSize;
    }

    private sealed record Row(List<UIElement> Children, double Width, double Height);

    // Splits the children into rows that fit the width; a child wider than the width gets a row of its own.
    private List<Row> Rows(double maxWidth, bool measure)
    {
        var rows = new List<Row>();
        var current = new List<UIElement>();
        double width = 0, height = 0;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            if (measure)
            {
                child.Measure(new Size(maxWidth, double.PositiveInfinity));
            }

            var size = child.DesiredSize;
            var needed = current.Count == 0 ? size.Width : width + HorizontalSpacing + size.Width;
            if (current.Count > 0 && needed > maxWidth)
            {
                rows.Add(new Row(current, width, height));
                current = [];
                width = height = 0;
                needed = size.Width;
            }

            current.Add(child);
            width = needed;
            height = Math.Max(height, size.Height);
        }

        if (current.Count > 0)
        {
            rows.Add(new Row(current, width, height));
        }

        return rows;
    }
}
