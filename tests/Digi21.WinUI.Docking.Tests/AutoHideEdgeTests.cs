using Microsoft.UI.Xaml.Controls;
using Xunit;

namespace Digi21.WinUI.Docking.Tests;

public class AutoHideEdgeTests
{
    [Theory]
    [InlineData(0, 2, DockSide.Left)]
    [InlineData(1, 2, DockSide.Right)]
    [InlineData(0, 3, DockSide.Left)]
    [InlineData(2, 3, DockSide.Right)]
    public void HorizontalSplit_CollapsesToTheNearestSide(int index, int count, DockSide expected)
    {
        Assert.Equal(expected, LayoutManager.EdgeOfPane(Orientation.Horizontal, index, count));
    }

    [Theory]
    [InlineData(0, 2, DockSide.Top)]
    [InlineData(1, 2, DockSide.Bottom)]
    [InlineData(0, 3, DockSide.Top)]
    [InlineData(2, 3, DockSide.Bottom)]
    public void VerticalSplit_CollapsesToTheNearestSide(int index, int count, DockSide expected)
    {
        Assert.Equal(expected, LayoutManager.EdgeOfPane(Orientation.Vertical, index, count));
    }

    [Fact]
    public void MiddlePane_CollapsesToTheTrailingSide()
    {
        // The middle of three is equally far from both, and a tie goes to the trailing edge.
        Assert.Equal(DockSide.Right, LayoutManager.EdgeOfPane(Orientation.Horizontal, 1, 3));
        Assert.Equal(DockSide.Bottom, LayoutManager.EdgeOfPane(Orientation.Vertical, 1, 3));
    }

    [Theory]
    [InlineData(1, 4, DockSide.Left)]
    [InlineData(2, 4, DockSide.Right)]
    public void EveryPane_CollapsesToTheSideOfTheSplitItSitsIn(int index, int count, DockSide expected)
    {
        Assert.Equal(expected, LayoutManager.EdgeOfPane(Orientation.Horizontal, index, count));
    }
}
