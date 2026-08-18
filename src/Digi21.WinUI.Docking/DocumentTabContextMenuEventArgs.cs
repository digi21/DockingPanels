using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Which documents a call to <see cref="DocumentHost.CloseDocuments"/> closes.
/// </summary>
public enum DocumentCloseScope
{
    /// <summary>Every document, pinned ones included.</summary>
    All,

    /// <summary>Every document except those whose <see cref="DocumentWindow.IsPinned"/> is set.</summary>
    AllButPinned,

    /// <summary>
    /// Every document except the active one and those whose <see cref="DocumentWindow.IsPinned"/>
    /// is set.
    /// </summary>
    AllButActive,
}

/// <summary>
/// Event data for <see cref="DockSite.DocumentTabContextMenuOpening"/>, raised just before a
/// document tab shows its context menu.
/// </summary>
/// <remarks>
/// <see cref="Items"/> arrives filled with the entries the library provides — pin or unpin, and the
/// close commands — and whatever it holds when the handlers are done is what the menu shows. An
/// application adds its own commands to it, reorders them, or empties it to replace the menu
/// entirely.
/// </remarks>
public class DocumentTabContextMenuEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="DocumentTabContextMenuEventArgs"/> class.</summary>
    /// <param name="document">The document whose tab was right-clicked.</param>
    /// <param name="items">The entries the menu will show.</param>
    public DocumentTabContextMenuEventArgs(DocumentWindow document, IList<MenuFlyoutItemBase> items)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(items);

        Document = document;
        Items = items;
    }

    /// <summary>Gets the document whose tab was right-clicked.</summary>
    public DocumentWindow Document { get; }

    /// <summary>Gets the entries the menu will show, in order.</summary>
    public IList<MenuFlyoutItemBase> Items { get; }
}
