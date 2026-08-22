using Xunit;

namespace Digi21.WinUI.Docking.Tests;

public class DocumentTabOrderTests
{
    private const DocumentTabZone Pinned = DocumentTabZone.Pinned;
    private const DocumentTabZone Normal = DocumentTabZone.Normal;
    private const DocumentTabZone Preview = DocumentTabZone.Provisional;

    [Fact]
    public void EachBlockStartsWhereTheOneBeforeItEnds()
    {
        DocumentTabZone[] zones = [Pinned, Pinned, Normal, Normal, Preview];

        Assert.Equal(0, DocumentTabOrder.StartOf(zones, Pinned));
        Assert.Equal(2, DocumentTabOrder.EndOf(zones, Pinned));
        Assert.Equal(2, DocumentTabOrder.StartOf(zones, Normal));
        Assert.Equal(4, DocumentTabOrder.EndOf(zones, Normal));
        Assert.Equal(4, DocumentTabOrder.StartOf(zones, Preview));
        Assert.Equal(5, DocumentTabOrder.EndOf(zones, Preview));
    }

    [Fact]
    public void PinningATab_MovesItToTheEndOfThePinnedBlock()
    {
        // The third tab has just been pinned: it goes after the two that already were.
        Assert.Equal(2, DocumentTabOrder.TargetIndexFor([Pinned, Pinned, Pinned, Normal], 2));
    }

    [Fact]
    public void UnpinningATab_MovesItToTheFrontOfTheNormalBlock()
    {
        // The first of three pinned tabs has just been unpinned: it goes right after the other two.
        Assert.Equal(2, DocumentTabOrder.TargetIndexFor([Normal, Pinned, Pinned, Normal], 0));
    }

    [Fact]
    public void PreviewingATab_MovesItToTheEndOfTheStrip()
    {
        Assert.Equal(3, DocumentTabOrder.TargetIndexFor([Preview, Pinned, Normal, Normal], 0));
    }

    [Fact]
    public void PromotingThePreview_MovesItToTheEndOfTheNormalBlock()
    {
        // It was last and it stays last, because there is nothing after the normal block but itself.
        Assert.Equal(3, DocumentTabOrder.TargetIndexFor([Pinned, Normal, Normal, Normal], 3));
    }

    [Fact]
    public void PromotingThePreviewOfAGroupWithAnotherOne_PutsItBeforeThatOne()
    {
        // Only reachable for an instant, while two documents are provisional at once.
        Assert.Equal(2, DocumentTabOrder.TargetIndexFor([Pinned, Normal, Normal, Preview], 2));
    }

    [Fact]
    public void PinningTheOnlyTab_LeavesItWhereItIs()
    {
        Assert.Equal(0, DocumentTabOrder.TargetIndexFor([Pinned], 0));
    }

    [Fact]
    public void DraggingAPinnedTabPastTheBlock_KeepsItInTheBlock()
    {
        // Two pinned tabs and two normal ones; the first is dragged to the far right. Pinning stays
        // an explicit gesture, so the drag stops at the end of the pinned block instead of unpinning.
        Assert.Equal(1, DocumentTabOrder.ClampMove([Pinned, Pinned, Normal, Normal], 0, 3));
    }

    [Fact]
    public void DraggingANormalTabToTheFarLeft_StopsAfterThePinnedBlock()
    {
        Assert.Equal(2, DocumentTabOrder.ClampMove([Pinned, Pinned, Normal, Normal], 3, 0));
    }

    [Fact]
    public void DraggingANormalTabPastThePreview_StopsBeforeIt()
    {
        Assert.Equal(2, DocumentTabOrder.ClampMove([Pinned, Normal, Normal, Preview], 1, 3));
    }

    [Fact]
    public void DraggingThePreview_IsClampedToTheNormalBlock_BecauseTheDragPromotesIt()
    {
        // Dropped at the far left, it lands at the front of the normal block rather than staying
        // last: the drop has promoted it by the time the move is applied.
        Assert.Equal(1, DocumentTabOrder.ClampMove([Pinned, Normal, Normal, Preview], 3, 0));
        Assert.Equal(2, DocumentTabOrder.ClampMove([Pinned, Normal, Normal, Preview], 3, 2));
    }

    [Fact]
    public void DraggingWithinTheOwnBlock_IsLeftAlone()
    {
        Assert.Equal(2, DocumentTabOrder.ClampMove([Pinned, Pinned, Normal, Normal], 3, 2));
        Assert.Equal(0, DocumentTabOrder.ClampMove([Pinned, Pinned, Normal, Normal], 1, 0));
    }

    [Fact]
    public void DraggingInAGroupWithoutBlocks_IsNeverClamped()
    {
        Assert.Equal(3, DocumentTabOrder.ClampMove([Normal, Normal, Normal, Normal], 0, 3));
    }

    [Fact]
    public void AppendingAPinnedDocument_LandsAtTheEndOfThePinnedBlock()
    {
        Assert.Equal(2, DocumentTabOrder.ClampInsertion([Pinned, Pinned, Normal], Pinned, index: -1));
    }

    [Fact]
    public void AppendingANormalDocument_LandsBeforeThePreview()
    {
        // The provisional tab stays at the end of the strip without anything having to move it.
        Assert.Equal(3, DocumentTabOrder.ClampInsertion([Pinned, Pinned, Normal, Preview], Normal, index: -1));
    }

    [Fact]
    public void AppendingThePreview_LandsLast()
    {
        Assert.Equal(3, DocumentTabOrder.ClampInsertion([Pinned, Pinned, Normal], Preview, index: -1));
    }

    [Fact]
    public void DroppingADocumentInTheWrongBlock_IsClampedToItsOwn()
    {
        Assert.Equal(2, DocumentTabOrder.ClampInsertion([Pinned, Pinned, Normal], Pinned, index: 3));
        Assert.Equal(2, DocumentTabOrder.ClampInsertion([Pinned, Pinned, Normal], Normal, index: 0));
        Assert.Equal(3, DocumentTabOrder.ClampInsertion([Pinned, Pinned, Normal], Preview, index: 0));
    }

    [Fact]
    public void DroppingIntoAnEmptyGroup_LandsFirst()
    {
        Assert.Equal(0, DocumentTabOrder.ClampInsertion([], Pinned, index: -1));
        Assert.Equal(0, DocumentTabOrder.ClampInsertion([], Normal, index: 4));
        Assert.Equal(0, DocumentTabOrder.ClampInsertion([], Preview, index: 4));
    }

    [Fact]
    public void TabsAlreadyInBlocks_AreLeftAlone()
    {
        Assert.Null(DocumentTabOrder.Partition([Pinned, Pinned, Normal, Normal, Preview]));
        Assert.Null(DocumentTabOrder.Partition([Normal, Normal]));
        Assert.Null(DocumentTabOrder.Partition([]));
    }

    [Fact]
    public void MixedTabs_ArePartitionedKeepingTheOrderWithinEachBlock()
    {
        // What a hand-edited layout or an application inserting into Items itself can produce.
        var order = DocumentTabOrder.Partition([Normal, Pinned, Preview, Normal, Pinned]);

        Assert.Equal([1, 4, 0, 3, 2], Assert.IsType<int[]>(order));
    }
}
