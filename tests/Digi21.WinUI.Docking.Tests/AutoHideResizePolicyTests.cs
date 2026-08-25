using Digi21.WinUI.Docking.Interaction;
using Xunit;

namespace Digi21.WinUI.Docking.Tests;

public class AutoHideResizePolicyTests
{
    [Theory]
    [InlineData(DockSide.Left)]
    [InlineData(DockSide.Top)]
    public void PanelAtTheStartOfAnAxis_GrowsAsTheGripMovesAway(DockSide edge)
    {
        Assert.Equal(340, AutoHideResizePolicy.SizeFromDrag(edge, 300, 40));
        Assert.Equal(260, AutoHideResizePolicy.SizeFromDrag(edge, 300, -40));
    }

    [Theory]
    [InlineData(DockSide.Right)]
    [InlineData(DockSide.Bottom)]
    public void PanelAtTheEndOfAnAxis_GrowsAsTheGripMovesBack(DockSide edge)
    {
        // Its grip is on the other side of the panel, so the same movement means the opposite.
        Assert.Equal(260, AutoHideResizePolicy.SizeFromDrag(edge, 300, 40));
        Assert.Equal(340, AutoHideResizePolicy.SizeFromDrag(edge, 300, -40));
    }

    [Fact]
    public void Clamp_LeavesASizeTheLayoutCanGive()
    {
        Assert.Equal(300, AutoHideResizePolicy.Clamp(300, 1000));
    }

    [Fact]
    public void Clamp_KeepsAFifthOfTheAreaVisible()
    {
        Assert.Equal(800, AutoHideResizePolicy.Clamp(5000, 1000));
    }

    [Fact]
    public void Clamp_KeepsThePanelBigEnoughToUse()
    {
        Assert.Equal(AutoHideResizePolicy.MinimumSize, AutoHideResizePolicy.Clamp(0, 1000));
        Assert.Equal(AutoHideResizePolicy.MinimumSize, AutoHideResizePolicy.Clamp(-200, 1000));
    }

    [Fact]
    public void Clamp_PrefersTheMinimumOverAnAreaTooSmallToHoldIt()
    {
        // A dock site narrower than the smallest panel: the floor wins, and the panel covers it
        // rather than becoming a sliver nothing can be read in.
        Assert.Equal(AutoHideResizePolicy.MinimumSize, AutoHideResizePolicy.Clamp(300, 40));
    }

    [Fact]
    public void Clamp_RefusesASizeThatIsNotANumber()
    {
        // Nothing in the drag can produce one, but a layout file can carry one.
        Assert.Equal(AutoHideResizePolicy.MinimumSize, AutoHideResizePolicy.Clamp(double.NaN, 1000));
        Assert.Equal(AutoHideResizePolicy.MinimumSize, AutoHideResizePolicy.Clamp(double.PositiveInfinity, 1000));
    }
}
