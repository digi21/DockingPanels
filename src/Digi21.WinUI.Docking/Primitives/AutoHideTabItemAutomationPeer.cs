using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// Makes the tab of an auto-hidden tool window announce itself as one of the tabs of its strip, and
/// lets its panel be brought out without a mouse.
/// </summary>
/// <remarks>
/// An auto-hidden window is the one an automated test can least afford to be unable to reach: until
/// its flyout is open its content is not in the visual tree at all, and the tab is the only way in.
/// Choosing the tab does what clicking it does — it opens the flyout and makes the window active —
/// and deselecting it puts the panel away again.
/// </remarks>
public partial class AutoHideTabItemAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider, ISelectionItemProvider
{
    /// <summary>Initializes a new instance of the <see cref="AutoHideTabItemAutomationPeer"/> class.</summary>
    /// <param name="owner">The tab this peer speaks for.</param>
    public AutoHideTabItemAutomationPeer(AutoHideTabItem owner)
        : base(owner)
    {
    }

    /// <summary>Gets a value indicating whether this tab's panel is the one the flyout is showing.</summary>
    public bool IsSelected => Tab.Window is { } window && ReferenceEquals(Site?.AutoHideFlyoutWindow, window);

    /// <inheritdoc/>
    public IRawElementProviderSimple? SelectionContainer =>
        Tab.FindAncestor<AutoHideTabStrip>() is { } strip && CreatePeerForElement(strip) is { } peer
            ? ProviderFromPeer(peer)
            : null;

    private AutoHideTabItem Tab => (AutoHideTabItem)Owner;

    private DockSite? Site => Tab.FindAncestor<DockSite>();

    /// <summary>Opens the window's flyout and makes it active, as clicking the tab does.</summary>
    public void Invoke() => Tab.Window?.Activate();

    /// <summary>Opens the window's flyout and makes it active, as clicking the tab does.</summary>
    public void Select() => Tab.Window?.Activate();

    /// <summary>Opens the window's flyout and makes it active, as clicking the tab does.</summary>
    /// <remarks>A strip shows one panel at a time, so adding to the selection is choosing.</remarks>
    public void AddToSelection() => Tab.Window?.Activate();

    /// <summary>Puts the window's panel away, as the pointer leaving it does. Does nothing when it is not the one on show.</summary>
    public void RemoveFromSelection()
    {
        if (IsSelected)
        {
            Site?.HideAutoHideFlyout();
        }
    }

    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

    /// <inheritdoc/>
    protected override string GetClassNameCore() => nameof(AutoHideTabItem);

    /// <inheritdoc/>
    protected override string GetNameCore()
    {
        // The tab already carries its window's title as its automation name; the fallback is for
        // the moment before a template has been applied.
        var name = base.GetNameCore();

        return string.IsNullOrEmpty(name) ? Tab.Window?.Title ?? string.Empty : name;
    }

    /// <inheritdoc/>
    protected override object? GetPatternCore(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.Invoke or PatternInterface.SelectionItem
            ? this
            : base.GetPatternCore(patternInterface);
}
