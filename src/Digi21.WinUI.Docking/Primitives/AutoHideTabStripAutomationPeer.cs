using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// Makes the strip of auto-hide tabs at a dock site edge announce itself as a set of tabs, and
/// tells an automation client which of them, if any, is showing its panel.
/// </summary>
/// <remarks>
/// Unlike the tabs of a pane, these need no selection at all: the usual state of an edge is that
/// every panel on it is put away.
/// </remarks>
public partial class AutoHideTabStripAutomationPeer : FrameworkElementAutomationPeer, ISelectionProvider
{
    /// <summary>Initializes a new instance of the <see cref="AutoHideTabStripAutomationPeer"/> class.</summary>
    /// <param name="owner">The strip this peer speaks for.</param>
    public AutoHideTabStripAutomationPeer(AutoHideTabStrip owner)
        : base(owner)
    {
    }

    /// <summary>Gets a value indicating whether several tabs can be selected at once. They cannot.</summary>
    public bool CanSelectMultiple => false;

    /// <summary>Gets a value indicating whether a tab is always selected. None need be.</summary>
    public bool IsSelectionRequired => false;

    private AutoHideTabStrip Strip => (AutoHideTabStrip)Owner;

    /// <summary>Gets the tab of the panel the flyout is showing, or nothing while none is out.</summary>
    /// <returns>The selected tab, as the single element of the array.</returns>
    public IRawElementProviderSimple[] GetSelection()
    {
        if (Strip.FindAncestor<DockSite>()?.AutoHideFlyoutWindow is not { } shown)
        {
            return [];
        }

        foreach (var tab in Strip.Tabs)
        {
            if (ReferenceEquals(tab.Window, shown) && CreatePeerForElement(tab) is { } peer)
            {
                return [ProviderFromPeer(peer)];
            }
        }

        return [];
    }

    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

    /// <inheritdoc/>
    protected override string GetClassNameCore() => nameof(AutoHideTabStrip);

    /// <inheritdoc/>
    protected override object? GetPatternCore(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.Selection
            ? this
            : base.GetPatternCore(patternInterface);
}
