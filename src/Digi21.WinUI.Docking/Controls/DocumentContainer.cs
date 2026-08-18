using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking;

/// <summary>
/// A tab group of the document area: hosts <see cref="DocumentWindow"/> instances as tabs shown
/// along the top of the group, like the editor tabs of Visual Studio.
/// </summary>
/// <remarks>
/// Several groups can live side by side inside a <see cref="DocumentHost"/>, split by
/// <see cref="SplitContainer"/> panes; dragging a document tab onto a side guide of a group
/// creates a new one. Unlike a <see cref="ToolWindowContainer"/>, a document group shows its
/// tabs even when it holds a single document, and has no title bar. Documents whose
/// <see cref="DocumentWindow.IsPinned"/> is set keep their own block at the head of the group.
/// </remarks>
public partial class DocumentContainer : DockingWindowContainer
{
    private bool partitioning;

    /// <summary>Initializes a new instance of the <see cref="DocumentContainer"/> class.</summary>
    public DocumentContainer()
    {
        DefaultStyleKey = typeof(DocumentContainer);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");

        // The templates below reference the library's own brush and metric keys; this is what
        // puts them within reach of the application's resources.
        DockingThemeResources.Ensure();
    }

    /// <inheritdoc />
    protected override Control CreateTab(DockingWindow window) => new DocumentTabItem { Window = window };

    /// <summary>Document tabs are always shown: they are how a document is named and closed.</summary>
    /// <param name="count">The number of documents currently hosted.</param>
    protected override bool ShowTabs(int count) => count > 0;

    /// <summary>A pinned document's tab goes into the fixed strip, ahead of the scrolling one.</summary>
    /// <param name="window">The window the tab represents.</param>
    protected override bool BelongsToPinnedStrip(DockingWindow window)
        => window is DocumentWindow { IsPinned: true };

    /// <summary>
    /// Puts the pinned documents back at the head of the group, keeping the order within each
    /// block, for the case where they were inserted into <see cref="DockingWindowContainer.Items"/>
    /// directly instead of through a docking operation.
    /// </summary>
    protected override void CoerceItemOrder()
    {
        if (partitioning || DocumentTabOrder.Partition(PinnedFlags()) is not { } order)
        {
            return;
        }

        var reordered = new DockingWindow[order.Length];
        for (var i = 0; i < order.Length; i++)
        {
            reordered[i] = Items[order[i]];
        }

        // Taking a window out of Items leaves the container without a selection for an instant, and
        // it falls back to whichever tab is at that position: reordering must not change which
        // document is shown.
        var selected = SelectedItem;

        partitioning = true;
        try
        {
            for (var i = 0; i < reordered.Length; i++)
            {
                var window = reordered[i];
                var from = Items.IndexOf(window);
                if (from == i)
                {
                    continue;
                }

                // Taking a window out of the collection and putting it back is not a close and a
                // reopen, and the application must not hear about it as one.
                window.IsRelocating = true;
                Items.RemoveAt(from);
                Items.Insert(i, window);
                window.IsRelocating = false;
            }
        }
        finally
        {
            partitioning = false;

            if (selected is not null)
            {
                Select(selected);
            }
        }
    }

    // Gets the pinned flag of every tab, in tab order, which is what the ordering rules work on.
    internal bool[] PinnedFlags()
    {
        var flags = new bool[Items.Count];

        for (var i = 0; i < flags.Length; i++)
        {
            flags[i] = Items[i] is DocumentWindow { IsPinned: true };
        }

        return flags;
    }
}
