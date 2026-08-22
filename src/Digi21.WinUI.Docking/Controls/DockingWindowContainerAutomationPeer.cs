using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Makes a pane of docking windows announce itself as a set of tabs, and tells an automation client
/// which of them is the one on show.
/// </summary>
/// <remarks>
/// This is the selection container the tabs of the pane point at, so a client that finds a tab can
/// walk to the pane it belongs to and read its selection, rather than having to compare tabs one by
/// one. Only the windows shown as tabs count: a <see cref="ToolWindowContainer"/> holding a single
/// window shows none, and reports an empty selection.
/// </remarks>
public partial class DockingWindowContainerAutomationPeer : FrameworkElementAutomationPeer, ISelectionProvider
{
    /// <summary>Initializes a new instance of the <see cref="DockingWindowContainerAutomationPeer"/> class.</summary>
    /// <param name="owner">The pane this peer speaks for.</param>
    public DockingWindowContainerAutomationPeer(DockingWindowContainer owner)
        : base(owner)
    {
    }

    /// <summary>Gets a value indicating whether several tabs can be selected at once. They cannot.</summary>
    public bool CanSelectMultiple => false;

    /// <summary>Gets a value indicating whether one tab is always selected.</summary>
    /// <remarks>
    /// A pane showing tabs always has one of them on show; while it shows none there is nothing to
    /// require.
    /// </remarks>
    public bool IsSelectionRequired => GetSelection().Length > 0;

    private DockingWindowContainer Container => (DockingWindowContainer)Owner;

    /// <summary>Gets the tab of the window the pane is showing, or nothing while it shows no tabs.</summary>
    /// <returns>The selected tab, as the single element of the array.</returns>
    public IRawElementProviderSimple[] GetSelection()
    {
        if (Container.SelectedItem is not { } selected
            || Container.TabFor(selected) is not { } tab
            || CreatePeerForElement(tab) is not { } peer)
        {
            return [];
        }

        return [ProviderFromPeer(peer)];
    }

    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

    /// <inheritdoc/>
    protected override string GetClassNameCore() => Owner.GetType().Name;

    /// <inheritdoc/>
    protected override object? GetPatternCore(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.Selection
            ? this
            : base.GetPatternCore(patternInterface);
}
