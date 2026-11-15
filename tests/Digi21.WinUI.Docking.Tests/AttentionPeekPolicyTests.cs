using Digi21.WinUI.Docking.Interaction;
using Xunit;

namespace Digi21.WinUI.Docking.Tests;

public class AttentionPeekPolicyTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    [Fact]
    public void IsEnabled_WithTheDefaults()
    {
        Assert.True(AttentionPeekPolicy.IsEnabled(2, 40, TimeSpan.FromMilliseconds(500)));
    }

    [Theory]
    [InlineData(0, 40.0, 500)]
    [InlineData(-1, 40.0, 500)]
    [InlineData(2, 0.0, 500)]
    [InlineData(2, double.NaN, 500)]
    [InlineData(2, double.PositiveInfinity, 500)]
    [InlineData(2, 40.0, 0)]
    public void IsEnabled_AnyOfTheThreeAtNothingTurnsItOff(int count, double distance, int milliseconds)
    {
        Assert.False(AttentionPeekPolicy.IsEnabled(count, distance, TimeSpan.FromMilliseconds(milliseconds)));
    }

    [Fact]
    public void IsDue_AFreshRequestPeeksEvenRightAfterTheLastPeek()
    {
        // The tab was unlit: the user has seen the content since, so this is news.
        Assert.True(AttentionPeekPolicy.IsDue(alreadyRequested: false, TimeSpan.FromMilliseconds(100), Interval));
    }

    [Fact]
    public void IsDue_ALitTabThatNeverPeekedPeeks()
    {
        // A peek that was refused for another reason, such as another panel being out at the edge,
        // is owed at the next request.
        Assert.True(AttentionPeekPolicy.IsDue(alreadyRequested: true, sinceLastPeek: null, Interval));
    }

    [Fact]
    public void IsDue_ALitTabWaitsForTheInterval()
    {
        Assert.False(AttentionPeekPolicy.IsDue(alreadyRequested: true, TimeSpan.FromSeconds(9.9), Interval));
        Assert.True(AttentionPeekPolicy.IsDue(alreadyRequested: true, TimeSpan.FromSeconds(10), Interval));
    }

    [Fact]
    public void IsDue_FiftyRequestsInASecondPeekOnce()
    {
        // Content that asks on every write: the first request lights the tab and peeks, the other
        // forty-nine find it lit and within the interval.
        var peeks = 0;
        TimeSpan? lastPeek = null;
        var lit = false;

        for (var i = 0; i < 50; i++)
        {
            var now = TimeSpan.FromMilliseconds(i * 20);
            if (AttentionPeekPolicy.IsDue(lit, now - lastPeek, Interval))
            {
                peeks++;
                lastPeek = now;
            }

            lit = true;
        }

        Assert.Equal(1, peeks);
    }
}
