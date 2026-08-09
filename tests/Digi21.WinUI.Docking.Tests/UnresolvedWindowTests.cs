using Digi21.WinUI.Docking.Serialization;
using Xunit;

namespace Digi21.WinUI.Docking.Tests;

public class UnresolvedWindowTests
{
    [Fact]
    public void Close_ClosesWindowsTheLayoutDoesNotMention()
    {
        Assert.False(DockSiteLayoutSerializer.KeepsUnresolvedWindowOpen(
            UnresolvedWindowBehavior.Close, canClose: true));
    }

    [Fact]
    public void Close_KeepsWindowsTheUserCannotClose()
    {
        // A window declared with CanClose="False" cannot be closed from the interface, so there
        // would be no way to bring it back after a layout that does not mention it had closed it.
        Assert.True(DockSiteLayoutSerializer.KeepsUnresolvedWindowOpen(
            UnresolvedWindowBehavior.Close, canClose: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DockLeft_KeepsEveryWindowOpen(bool canClose)
    {
        Assert.True(DockSiteLayoutSerializer.KeepsUnresolvedWindowOpen(
            UnresolvedWindowBehavior.DockLeft, canClose));
    }
}
