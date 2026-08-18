using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Windows.Foundation;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Base class of the panes that host docking windows as tabs: <see cref="ToolWindowContainer"/>
/// for tool windows and <see cref="DocumentContainer"/> for documents. All hosted windows stay
/// loaded and switching tabs only toggles visibility, so control state is preserved.
/// </summary>
/// <remarks>
/// Both kinds of pane are interchangeable as far as the layout tree is concerned: they are the
/// leaves of a tree of <see cref="SplitContainer"/> panes, they take drops, and every layout
/// mutation moves windows between them. What each subclass decides is how its tabs look and
/// when they are shown.
/// </remarks>
[ContentProperty(Name = nameof(Items))]
public abstract partial class DockingWindowContainer : Control
{
    /// <summary>Identifies the <see cref="SelectedIndex"/> dependency property.</summary>
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex),
        typeof(int),
        typeof(DockingWindowContainer),
        new PropertyMetadata(-1, (d, _) => ((DockingWindowContainer)d).OnSelectedIndexChanged()));

    /// <summary>Identifies the <see cref="SelectedItem"/> dependency property.</summary>
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(DockingWindow),
        typeof(DockingWindowContainer),
        new PropertyMetadata(null, (d, _) => ((DockingWindowContainer)d).OnSelectedItemChanged()));

    private readonly ObservableCollection<DockingWindow> items = [];

    // The windows are hosted in a grid OWNED by this container, not by its template: templates
    // are re-applied when the container is disconnected and reconnected by layout mutations,
    // and windows still associated with a discarded template's panel cannot be re-added
    // elsewhere (COMException 0x800F1000). The template only provides a ContentPresenter slot
    // that this grid is plugged into.
    private readonly Grid windowsHost = new();
    private ContentPresenter? contentSlot;
    private Panel? pinnedTabStrip;
    private Panel? tabStrip;
    private ToolWindowTitleBar? titleBar;
    private bool syncingSelection;

    /// <summary>Initializes a new instance of the <see cref="DockingWindowContainer"/> class.</summary>
    protected DockingWindowContainer()
    {
        items.CollectionChanged += OnItemsChanged;
    }

    // Raised after windows have been added to or removed from Items.
    internal event EventHandler? ItemsChanged;

    /// <summary>Gets the windows hosted by this container, in tab order.</summary>
    public IList<DockingWindow> Items => items;

    /// <summary>Gets or sets the index of the selected (visible) window, or -1 when empty.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Gets or sets the selected (visible) window.</summary>
    public DockingWindow? SelectedItem
    {
        get => (DockingWindow?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    // Rebuilds the tabs, for a change that alters how a window is shown without altering Items.
    internal void RefreshTabs() => Rebuild();

    // Makes the given window the selected tab of this container.
    internal void Select(DockingWindow window)
    {
        if (items.Contains(window))
        {
            SelectedItem = window;
        }
    }

    /// <summary>Creates the tab control shown for a hosted window.</summary>
    /// <param name="window">The window the tab represents.</param>
    protected abstract Control CreateTab(DockingWindow window);

    /// <summary>Tells whether the tab strip is shown for the given number of hosted windows.</summary>
    /// <param name="count">The number of windows currently hosted.</param>
    protected abstract bool ShowTabs(int count);

    /// <summary>
    /// Tells whether a window's tab goes into the fixed strip that a template can declare as
    /// <c>PART_PinnedTabStrip</c>, ahead of the scrolling one, instead of into the scrolling strip.
    /// </summary>
    /// <param name="window">The window the tab represents.</param>
    /// <remarks>
    /// Only <see cref="DocumentContainer"/> answers <see langword="true"/>, for the documents whose
    /// tab is pinned. A template without that part puts every tab in the scrolling strip.
    /// </remarks>
    protected virtual bool BelongsToPinnedStrip(DockingWindow window) => false;

    /// <summary>
    /// Puts <see cref="Items"/> back in the order this kind of container keeps them in, before the
    /// selection and the tabs are brought up to date. Called whenever the collection changes.
    /// </summary>
    protected virtual void CoerceItemOrder()
    {
    }

    // Gets a value indicating whether this container's own title bar can act as the caption of a
    // floating window holding nothing but it. A floating window whose only pane has no caption of
    // its own gets one, or it could not be moved.
    internal virtual bool ProvidesWindowCaption => false;

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Release the owned windows host from the previous template's slot (the old presenter
        // reference is kept exactly for this) before plugging it into the new one.
        if (contentSlot is not null)
        {
            contentSlot.Content = null;
        }

        contentSlot = GetTemplateChild("PART_ContentHost") as ContentPresenter;
        if (contentSlot is not null)
        {
            contentSlot.Content = windowsHost;
        }

        pinnedTabStrip = GetTemplateChild("PART_PinnedTabStrip") as Panel;
        tabStrip = GetTemplateChild("PART_TabStrip") as Panel;
        titleBar = GetTemplateChild("PART_TitleBar") as ToolWindowTitleBar;

        Rebuild();
    }

    // Gets the bounds of the tab strip in this container's coordinates, or an empty rectangle when
    // no strip is shown. A container whose template splits the tabs into a pinned strip and a
    // scrolling one reports both together: as far as a drag is concerned they are one strip.
    internal Rect TabStripBounds
    {
        get
        {
            var bounds = Rect.Empty;

            foreach (var strip in Strips())
            {
                bounds.Union(BoundsOf(strip));
            }

            return bounds;
        }
    }

    // Enumerates the strips holding the tabs, pinned first, skipping those with nothing to show.
    private IEnumerable<Panel> Strips()
    {
        foreach (var strip in new[] { pinnedTabStrip, tabStrip })
        {
            if (strip is { Visibility: Visibility.Visible, ActualWidth: > 0, ActualHeight: > 0 })
            {
                yield return strip;
            }
        }
    }

    // Gets the tab position a window dropped at the given point would take, so dragging a tab along
    // the strip reorders it instead of re-docking the pane.
    //
    // point: A point in this container's coordinates.
    internal int InsertionIndexAt(Point point)
    {
        var tabs = TabBounds();
        for (var i = 0; i < tabs.Count; i++)
        {
            if (point.X < tabs[i].X + (tabs[i].Width / 2))
            {
                return i;
            }
        }

        return tabs.Count;
    }

    // Gets the caret drawn between tabs to show where a dragged tab would land, in this container's
    // coordinates.
    //
    // index: The insertion position, between 0 and the number of hosted windows.
    internal Rect InsertionMarker(int index)
    {
        const double thickness = 2.0;

        var strip = TabStripBounds;
        if (strip.IsEmpty)
        {
            return Rect.Empty;
        }

        var tabs = TabBounds();
        var x = tabs.Count == 0
            ? strip.X
            : index <= 0
                ? tabs[0].X
                : index >= tabs.Count
                    ? tabs[^1].X + tabs[^1].Width
                    : tabs[index].X;

        return new Rect(x - (thickness / 2), strip.Y, thickness, strip.Height);
    }

    // Gets the bounds of the tabs, in this container's coordinates and in tab order.
    //
    // The pinned strip comes first, which is both where it is drawn and — because Items keeps the
    // pinned windows at its head — the same order as Items, so an index into this list is an index
    // into Items.
    private List<Rect> TabBounds()
    {
        var bounds = new List<Rect>(items.Count);

        foreach (var strip in Strips())
        {
            foreach (var child in strip.Children)
            {
                if (child is FrameworkElement tab)
                {
                    bounds.Add(BoundsOf(tab));
                }
            }
        }

        return bounds;
    }

    private Rect BoundsOf(FrameworkElement element)
    {
        return element
            .TransformToVisual(this)
            .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DockingWindow window in e.OldItems)
            {
                if (ReferenceEquals(window.Container, this))
                {
                    window.Container = null;
                }

                window.NotifyDetached();
            }
        }

        if (e.NewItems is not null)
        {
            foreach (DockingWindow window in e.NewItems)
            {
                window.Container?.Items.Remove(window);
                window.Container = this;
                window.NotifyAttached();
            }
        }

        CoerceItemOrder();
        CoerceSelection();
        Rebuild();
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CoerceSelection()
    {
        syncingSelection = true;
        try
        {
            if (items.Count == 0)
            {
                SelectedIndex = -1;
                SelectedItem = null;
                return;
            }

            var selected = SelectedItem;
            if (selected is null || !items.Contains(selected))
            {
                var index = Math.Clamp(SelectedIndex, 0, items.Count - 1);
                SelectedIndex = index;
                SelectedItem = items[index];
            }
            else
            {
                SelectedIndex = items.IndexOf(selected);
            }
        }
        finally
        {
            syncingSelection = false;
        }
    }

    private void OnSelectedIndexChanged()
    {
        if (syncingSelection)
        {
            return;
        }

        syncingSelection = true;
        try
        {
            var index = SelectedIndex;
            SelectedItem = index >= 0 && index < items.Count ? items[index] : null;
        }
        finally
        {
            syncingSelection = false;
        }

        ApplySelection();
    }

    private void OnSelectedItemChanged()
    {
        if (!syncingSelection)
        {
            syncingSelection = true;
            try
            {
                var item = SelectedItem;
                SelectedIndex = item is null ? -1 : items.IndexOf(item);
            }
            finally
            {
                syncingSelection = false;
            }
        }

        ApplySelection();
    }

    // Synchronizes the visual parts (content host, tab strip, title bar) with Items.
    private void Rebuild()
    {
        for (var i = windowsHost.Children.Count - 1; i >= 0; i--)
        {
            if (windowsHost.Children[i] is DockingWindow window && !items.Contains(window))
            {
                windowsHost.Children.RemoveAt(i);
            }
        }

        foreach (var window in items)
        {
            if (!windowsHost.Children.Contains(window))
            {
                windowsHost.Children.Add(window);
            }
        }

        pinnedTabStrip?.Children.Clear();
        tabStrip?.Children.Clear();

        if (ShowTabs(items.Count))
        {
            foreach (var window in items)
            {
                var strip = pinnedTabStrip is not null && BelongsToPinnedStrip(window) ? pinnedTabStrip : tabStrip;
                strip?.Children.Add(CreateTab(window));
            }
        }

        // An empty pinned strip is collapsed rather than left at zero width, so its column takes no
        // room at all while no tab is pinned.
        SetStripVisibility(pinnedTabStrip, pinnedTabStrip?.Children.Count > 0);
        SetStripVisibility(tabStrip, ShowTabs(items.Count));

        ApplySelection();
    }

    private static void SetStripVisibility(Panel? strip, bool visible)
    {
        if (strip is not null)
        {
            strip.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ApplySelection()
    {
        var selected = SelectedItem;

        foreach (var window in items)
        {
            var isSelected = ReferenceEquals(window, selected);
            window.IsSelected = isSelected;
            window.Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed;
        }

        if (titleBar is not null)
        {
            titleBar.Window = selected;
            titleBar.Visibility = selected is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
