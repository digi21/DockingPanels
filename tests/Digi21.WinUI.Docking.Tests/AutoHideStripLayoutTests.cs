using Digi21.WinUI.Docking.Primitives;
using Xunit;

namespace Digi21.WinUI.Docking.Tests;

public class AutoHideStripLayoutTests
{
    private const double Gap = AutoHideStripLayout.GroupGap;

    [Fact]
    public void GroupsThatFit_StayWhereTheirPanelWas()
    {
        var positions = Place(900, (0d, 60d), (400d, 80d));

        Assert.Equal(new[] { 0d, 400d }, positions);
    }

    [Fact]
    public void GroupsAskingForTheSameSpot_AreStackedInsteadOfOverlapping()
    {
        // Two panels unpinned before the layout had been measured both report an offset of 0.
        var positions = Place(900, (0d, 60d), (0d, 80d));

        Assert.Equal(new[] { 0d, 60d + Gap }, positions);
    }

    [Fact]
    public void AGroupWishingForATakenSpot_IsPushedPastTheOneBeforeIt()
    {
        var positions = Place(900, (0d, 100d), (40d, 50d));

        Assert.Equal(new[] { 0d, 100d + Gap }, positions);
    }

    [Fact]
    public void GroupsAreOrderedByWhereTheyAskedToBe_NotByWhenTheyWereAdded()
    {
        var positions = Place(900, (500d, 60d), (100d, 40d));

        Assert.Equal(new[] { 500d, 100d }, positions);
    }

    [Fact]
    public void AGroupPastTheEndOfTheStrip_IsPulledBackInside()
    {
        var positions = Place(900, (0d, 60d), (880d, 80d));

        Assert.Equal(new[] { 0d, 820d }, positions);
    }

    [Fact]
    public void PullingGroupsBackKeepsThemApart()
    {
        var positions = Place(900, (700d, 200d), (880d, 200d));

        Assert.Equal(new[] { 492d, 700d }, positions);
    }

    [Fact]
    public void MoreTabsThanEdge_StartAtTheCornerAndLetTheTailRunOff()
    {
        var positions = Place(400, (0d, 300d), (0d, 300d), (0d, 300d));

        Assert.Equal(new[] { 0d, 300d + Gap, 600d + (2 * Gap) }, positions);
    }

    [Fact]
    public void AnUnmeasuredStrip_PlacesGroupsWithoutPullingThemBack()
    {
        var positions = Place(double.PositiveInfinity, (0d, 60d), (400d, 80d));

        Assert.Equal(new[] { 0d, 400d }, positions);
    }

    [Fact]
    public void NonsenseOffsetsAndLengths_CountAsZero()
    {
        var positions = Place(900, (double.NaN, 60d), (-10d, double.NaN));

        Assert.Equal(new[] { 0d, 60d + Gap }, positions);
    }

    [Fact]
    public void NoGroups_PlacesNothing()
    {
        Assert.Empty(AutoHideStripLayout.Place([], 900));
    }

    private static double[] Place(double available, params (double Hint, double Length)[] groups)
    {
        return AutoHideStripLayout.Place(groups, available);
    }
}
