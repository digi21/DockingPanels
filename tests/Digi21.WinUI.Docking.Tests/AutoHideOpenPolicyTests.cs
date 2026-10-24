using Digi21.WinUI.Docking.Interaction;
using Xunit;

namespace Digi21.WinUI.Docking.Tests;

public class AutoHideOpenPolicyTests
{
    [Fact]
    public void Pointer_OpensOnHover()
    {
        Assert.True(AutoHideOpenPolicy.ShouldOpen(AutoHideOpenTrigger.Pointer, AutoHideOpenReason.Hover));
    }

    [Fact]
    public void Click_IgnoresHover()
    {
        // The whole point of the setting: the pointer crossing the edge leaves the layout alone.
        Assert.False(AutoHideOpenPolicy.ShouldOpen(AutoHideOpenTrigger.Click, AutoHideOpenReason.Hover));
    }

    [Theory]
    [InlineData(AutoHideOpenTrigger.Pointer)]
    [InlineData(AutoHideOpenTrigger.Click)]
    public void Activation_OpensWhateverTheTriggerIs(AutoHideOpenTrigger trigger)
    {
        // Clicking the tab, DockingWindow.Activate and a UI Automation client selecting the tab all
        // arrive as an activation: none of them is hovering over anything, so none is refused.
        Assert.True(AutoHideOpenPolicy.ShouldOpen(trigger, AutoHideOpenReason.Activation));
    }

    [Fact]
    public void HoverDelay_WaitsWhenNoPanelIsOut()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(400), AutoHideOpenPolicy.HoverDelay(TimeSpan.FromMilliseconds(400), panelShowing: false));
    }

    [Fact]
    public void HoverDelay_SwapsAtOnceWhenAPanelIsOut()
    {
        // Moving from one tab to the next while a panel is showing is going through the panels, not passing by.
        Assert.Equal(TimeSpan.Zero, AutoHideOpenPolicy.HoverDelay(TimeSpan.FromMilliseconds(400), panelShowing: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void HoverDelay_ZeroOrLessOpensAtOnce(int milliseconds)
    {
        Assert.Equal(TimeSpan.Zero, AutoHideOpenPolicy.HoverDelay(TimeSpan.FromMilliseconds(milliseconds), panelShowing: false));
    }
}
