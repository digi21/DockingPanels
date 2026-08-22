using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// Makes a tab of a <see cref="DockingWindowContainer"/> announce itself as one of a set of tabs,
/// and lets its window be brought to the front without a mouse.
/// </summary>
/// <remarks>
/// <para>
/// Serves both kinds of tab: a <see cref="ToolWindowTabItem"/> and a <see cref="DocumentTabItem"/>
/// differ in what they draw, not in what they are to an automation client.
/// </para>
/// <para>
/// Written because a tab without a peer of its own falls back to the one WinUI gives any control,
/// which answers to no pattern: the tab could be read by name but not chosen, and the windows
/// behind it are collapsed, so nothing inside them is in the automation tree either. A window that
/// is not at the front was therefore out of reach of an automated test altogether, which had to
/// click a screen coordinate to get at it.
/// </para>
/// <para>
/// Both patterns, because both are true: invoking a tab is what a driver reaches for, and being one
/// of a set exactly one of which is chosen is what a tab is. Choosing one does what clicking it
/// does — <see cref="DockingWindow.Activate"/> — so the window it stands for also becomes the
/// active window of its dock site.
/// </para>
/// </remarks>
public partial class DockingWindowTabItemAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider, ISelectionItemProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DockingWindowTabItemAutomationPeer"/> class for
    /// a tool window's tab.
    /// </summary>
    /// <param name="owner">The tab this peer speaks for.</param>
    public DockingWindowTabItemAutomationPeer(ToolWindowTabItem owner)
        : base(owner)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DockingWindowTabItemAutomationPeer"/> class for
    /// a document's tab.
    /// </summary>
    /// <param name="owner">The tab this peer speaks for.</param>
    public DockingWindowTabItemAutomationPeer(DocumentTabItem owner)
        : base(owner)
    {
    }

    /// <inheritdoc/>
    public bool IsSelected => Window?.IsSelected == true;

    /// <inheritdoc/>
    public IRawElementProviderSimple? SelectionContainer =>
        Window?.Container is { } container && CreatePeerForElement(container) is { } peer
            ? ProviderFromPeer(peer)
            : null;

    private DockingWindow? Window => (Owner as IDockingWindowTab)?.Window;

    // Tells a listening automation client that a tab has become the selected one of its container.
    // Called by the tab itself, which is what watches its window's selection.
    internal static void NotifySelected(Control tab)
    {
        if (!ListenerExists(AutomationEvents.SelectionItemPatternOnElementSelected))
        {
            return;
        }

        // Only a tab an automation client has already reached has a peer; creating one here for a
        // tab nobody is watching would be inventing an element to announce.
        FromElement(tab)?.RaiseAutomationEvent(AutomationEvents.SelectionItemPatternOnElementSelected);
    }

    /// <summary>Brings the tab's window to the front of its container, as clicking the tab does.</summary>
    public void Invoke() => Window?.Activate();

    /// <summary>Brings the tab's window to the front of its container, as clicking the tab does.</summary>
    public void Select() => Window?.Activate();

    /// <summary>Brings the tab's window to the front of its container, as clicking the tab does.</summary>
    /// <remarks>A container shows one window at a time, so adding to the selection is choosing.</remarks>
    public void AddToSelection() => Window?.Activate();

    /// <summary>Not supported: a container always shows one of its windows.</summary>
    /// <exception cref="InvalidOperationException">Always.</exception>
    public void RemoveFromSelection() =>
        throw new InvalidOperationException("A container always shows one of its windows, so a tab cannot be deselected on its own.");

    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

    /// <inheritdoc/>
    protected override string GetClassNameCore() => Owner.GetType().Name;

    /// <inheritdoc/>
    protected override string GetNameCore()
    {
        // The tab already carries its window's title as its automation name; the fallback is for
        // the moment before a template has been applied.
        var name = base.GetNameCore();

        return string.IsNullOrEmpty(name) ? Window?.Title ?? string.Empty : name;
    }

    /// <inheritdoc/>
    protected override object? GetPatternCore(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.Invoke or PatternInterface.SelectionItem
            ? this
            : base.GetPatternCore(patternInterface);
}
