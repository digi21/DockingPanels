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
}
