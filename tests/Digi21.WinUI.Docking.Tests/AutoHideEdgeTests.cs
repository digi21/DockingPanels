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
    public void MiddlePane_OfASplitWithoutAWorkArea_CollapsesToTheTrailingSide()
    {
        // The middle of three is equally far from both, and a tie goes to the trailing edge.
        Assert.Equal(DockSide.Right, LayoutManager.EdgeOfPane(Orientation.Horizontal, 1, 3, []));
        Assert.Equal(DockSide.Bottom, LayoutManager.EdgeOfPane(Orientation.Vertical, 1, 3, []));
    }

    [Fact]
    public void MiddlePane_BeforeTheWorkArea_CollapsesToTheLeadingSide()
    {
        // [tool | pane | workspace]: a window rescued to the far left must not push the pane next
        // to it over to the other side of the documents.
        Assert.Equal(DockSide.Left, LayoutManager.EdgeOfPane(Orientation.Horizontal, 1, 3, [2]));
        Assert.Equal(DockSide.Top, LayoutManager.EdgeOfPane(Orientation.Vertical, 1, 3, [2]));
    }

    [Fact]
    public void MiddlePane_AfterTheWorkArea_CollapsesToTheTrailingSide()
    {
        Assert.Equal(DockSide.Right, LayoutManager.EdgeOfPane(Orientation.Horizontal, 1, 3, [0]));
        Assert.Equal(DockSide.Bottom, LayoutManager.EdgeOfPane(Orientation.Vertical, 1, 3, [0]));
    }

    [Theory]
    [InlineData(1, 4, 0, DockSide.Right)]
    [InlineData(2, 4, 3, DockSide.Left)]
    public void Pane_CollapsesToTheSideOfTheWorkArea_OverTheCloserHalf(
        int index,
        int count,
        int workArea,
        DockSide expected)
    {
        Assert.Equal(expected, LayoutManager.EdgeOfPane(Orientation.Horizontal, index, count, [workArea]));
    }

    [Fact]
    public void Pane_BetweenTwoWorkAreas_CollapsesToTheCloserHalf()
    {
        Assert.Equal(DockSide.Left, LayoutManager.EdgeOfPane(Orientation.Horizontal, 1, 4, [0, 3]));
        Assert.Equal(DockSide.Right, LayoutManager.EdgeOfPane(Orientation.Horizontal, 1, 3, [0, 2]));
    }

    [Theory]
    [InlineData(0, 2, 1, DockSide.Left)]
    [InlineData(1, 2, 0, DockSide.Right)]
    [InlineData(0, 3, 1, DockSide.Left)]
    [InlineData(0, 3, 2, DockSide.Left)]
    [InlineData(2, 3, 0, DockSide.Right)]
    [InlineData(2, 3, 1, DockSide.Right)]
    public void OuterPanes_CollapseToTheirOwnSide_WhereverTheWorkAreaIs(
        int index,
        int count,
        int workArea,
        DockSide expected)
    {
        Assert.Equal(expected, LayoutManager.EdgeOfPane(Orientation.Horizontal, index, count, [workArea]));
    }

    [Theory]
    [InlineData(1, 4, DockSide.Left)]
    [InlineData(2, 4, DockSide.Right)]
    public void EveryPane_CollapsesToTheSideOfTheSplitItSitsIn(int index, int count, DockSide expected)
    {
        Assert.Equal(expected, LayoutManager.EdgeOfPane(Orientation.Horizontal, index, count));
    }
}
