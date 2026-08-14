using Digi21.WinUI.Docking.Interaction;
using Xunit;

namespace Digi21.WinUI.Docking.Tests;

public class AutoHideDismissPolicyTests
{
    [Fact]
    public void PointerLeavingAPreview_CollapsesIt()
    {
        Assert.True(ShouldCollapse(AutoHideOpenReason.Hover, AutoHideDismissTrigger.PointerLeft, focusInside: false));
    }

    [Fact]
    public void PointerLeavingAPanelOpenedByClick_LeavesItOpen()
    {
        // The pointer is not how a panel the user asked for goes away; the focus is.
        Assert.False(ShouldCollapse(AutoHideOpenReason.Activation, AutoHideDismissTrigger.PointerLeft, focusInside: false));
    }

    [Fact]
    public void PointerLeavingAPreviewBeingTypedInto_LeavesItOpen()
    {
        // Pointing at the tab opened it, but the focus is inside it now: it stopped being a preview
        // the moment a control in it took the focus, whatever opened it.
        Assert.False(ShouldCollapse(AutoHideOpenReason.Hover, AutoHideDismissTrigger.PointerLeft, focusInside: true));
    }

    [Fact]
    public void FocusMovingOut_CollapsesWhateverOpenedIt()
    {
        foreach (var reason in new[] { AutoHideOpenReason.Hover, AutoHideOpenReason.Activation })
        {
            Assert.True(ShouldCollapse(reason, AutoHideDismissTrigger.FocusMovedOutside, focusInside: false));
        }
    }

    [Fact]
    public void ClickingElsewhereWhileTheFocusStaysInside_LeavesItOpen()
    {
        // A click that takes no focus with it — empty chrome, a splitter, a control that refuses
        // focus — is not the user leaving the panel they are working in. This is the case that used
        // to close the panel being filled in.
        Assert.False(ShouldCollapse(AutoHideOpenReason.Activation, AutoHideDismissTrigger.PointerPressedOutside, focusInside: true));
    }

    [Fact]
    public void ClickingElsewhereWhenNothingInsideHoldsTheFocus_CollapsesIt()
    {
        Assert.True(ShouldCollapse(AutoHideOpenReason.Activation, AutoHideDismissTrigger.PointerPressedOutside, focusInside: false));
        Assert.True(ShouldCollapse(AutoHideOpenReason.Hover, AutoHideDismissTrigger.PointerPressedOutside, focusInside: false));
    }

    private static bool ShouldCollapse(AutoHideOpenReason reason, AutoHideDismissTrigger trigger, bool focusInside)
    {
        return AutoHideDismissPolicy.ShouldCollapse(reason, trigger, focusInside);
    }
}
