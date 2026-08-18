using Xunit;

namespace Digi21.WinUI.Docking.Tests;

public class DocumentTabOrderTests
{
    [Fact]
    public void PinnedCount_IsWhereThePinnedBlockEnds()
    {
        Assert.Equal(2, DocumentTabOrder.PinnedCount([true, true, false, false]));
        Assert.Equal(0, DocumentTabOrder.PinnedCount([false, false]));
        Assert.Equal(0, DocumentTabOrder.PinnedCount([]));
    }

    [Fact]
    public void PinningATab_MovesItToTheEndOfThePinnedBlock()
    {
        // The third tab has just been pinned: it goes after the two that already were.
        Assert.Equal(2, DocumentTabOrder.TargetIndexFor([true, true, true, false], 2));
    }

    [Fact]
    public void UnpinningATab_MovesItToTheFrontOfTheNormalBlock()
    {
        // The first of three pinned tabs has just been unpinned: it goes right after the other two.
        Assert.Equal(2, DocumentTabOrder.TargetIndexFor([false, true, true, false], 0));
    }

    [Fact]
    public void PinningTheOnlyTab_LeavesItWhereItIs()
    {
        Assert.Equal(0, DocumentTabOrder.TargetIndexFor([true], 0));
    }

    [Fact]
    public void DraggingAPinnedTabPastTheBlock_KeepsItInTheBlock()
    {
        // Two pinned tabs and two normal ones; the first is dragged to the far right. Pinning stays
        // an explicit gesture, so the drag stops at the end of the pinned block instead of unpinning.
        Assert.Equal(1, DocumentTabOrder.ClampMove([true, true, false, false], 0, 3));
    }

    [Fact]
    public void DraggingANormalTabToTheFarLeft_StopsAfterThePinnedBlock()
    {
        Assert.Equal(2, DocumentTabOrder.ClampMove([true, true, false, false], 3, 0));
    }

    [Fact]
    public void DraggingWithinTheOwnBlock_IsLeftAlone()
    {
        Assert.Equal(2, DocumentTabOrder.ClampMove([true, true, false, false], 3, 2));
        Assert.Equal(0, DocumentTabOrder.ClampMove([true, true, false, false], 1, 0));
    }

    [Fact]
    public void DraggingInAGroupWithoutPinnedTabs_IsNeverClamped()
    {
        Assert.Equal(3, DocumentTabOrder.ClampMove([false, false, false, false], 0, 3));
    }

    [Fact]
    public void AppendingAPinnedDocument_LandsAtTheEndOfThePinnedBlock()
    {
        Assert.Equal(2, DocumentTabOrder.ClampInsertion([true, true, false], isPinned: true, index: -1));
    }

    [Fact]
    public void AppendingANormalDocument_LandsAtTheEndOfTheStrip()
    {
        Assert.Equal(3, DocumentTabOrder.ClampInsertion([true, true, false], isPinned: false, index: -1));
    }

    [Fact]
    public void DroppingADocumentInTheWrongBlock_IsClampedToItsOwn()
    {
        Assert.Equal(2, DocumentTabOrder.ClampInsertion([true, true, false], isPinned: true, index: 3));
        Assert.Equal(2, DocumentTabOrder.ClampInsertion([true, true, false], isPinned: false, index: 0));
    }

    [Fact]
    public void DroppingIntoAnEmptyGroup_LandsFirst()
    {
        Assert.Equal(0, DocumentTabOrder.ClampInsertion([], isPinned: true, index: -1));
        Assert.Equal(0, DocumentTabOrder.ClampInsertion([], isPinned: false, index: 4));
    }

    [Fact]
    public void TabsAlreadyInTwoBlocks_AreLeftAlone()
    {
        Assert.Null(DocumentTabOrder.Partition([true, true, false, false]));
        Assert.Null(DocumentTabOrder.Partition([false, false]));
        Assert.Null(DocumentTabOrder.Partition([]));
    }

    [Fact]
    public void MixedTabs_ArePartitionedKeepingTheOrderWithinEachBlock()
    {
        // What a hand-edited layout or an application inserting into Items itself can produce.
        var order = DocumentTabOrder.Partition([false, true, false, true]);

        Assert.Equal([1, 3, 0, 2], Assert.IsType<int[]>(order));
    }
}
