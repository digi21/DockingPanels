using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Hosts one or more <see cref="ToolWindow"/> instances. A single window shows a title bar;
/// multiple windows additionally become tabs at the bottom of the container. All hosted windows
/// stay loaded and switching tabs only toggles visibility, so control state is preserved.
/// </summary>
[ContentProperty(Name = nameof(Items))]
public partial class ToolWindowContainer : Control
{
    /// <summary>Identifies the <see cref="SelectedIndex"/> dependency property.</summary>
    public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
        nameof(SelectedIndex),
        typeof(int),
        typeof(ToolWindowContainer),
        new PropertyMetadata(-1, (d, _) => ((ToolWindowContainer)d).OnSelectedIndexChanged()));

    /// <summary>Identifies the <see cref="SelectedItem"/> dependency property.</summary>
    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem),
        typeof(ToolWindow),
        typeof(ToolWindowContainer),
        new PropertyMetadata(null, (d, _) => ((ToolWindowContainer)d).OnSelectedItemChanged()));

    private readonly ObservableCollection<ToolWindow> items = [];
    private Grid? contentHost;
    private StackPanel? tabStrip;
    private ToolWindowTitleBar? titleBar;
    private bool syncingSelection;

    /// <summary>Initializes a new instance of the <see cref="ToolWindowContainer"/> class.</summary>
    public ToolWindowContainer()
    {
        DefaultStyleKey = typeof(ToolWindowContainer);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
        items.CollectionChanged += OnItemsChanged;
    }

    /// <summary>Gets the tool windows hosted by this container, in tab order.</summary>
    public IList<ToolWindow> Items => items;

    /// <summary>Gets or sets the index of the selected (visible) tool window, or -1 when empty.</summary>
    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Gets or sets the selected (visible) tool window.</summary>
    public ToolWindow? SelectedItem
    {
        get => (ToolWindow?)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>Makes the given window the selected tab of this container.</summary>
    internal void Select(ToolWindow window)
    {
        if (items.Contains(window))
        {
            SelectedItem = window;
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        contentHost = GetTemplateChild("PART_ContentHost") as Grid;
        tabStrip = GetTemplateChild("PART_TabStrip") as StackPanel;
        titleBar = GetTemplateChild("PART_TitleBar") as ToolWindowTitleBar;

        Rebuild();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ToolWindow window in e.OldItems)
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
            foreach (ToolWindow window in e.NewItems)
            {
                window.Container?.Items.Remove(window);
                window.Container = this;
                window.NotifyAttached();
            }
        }

        CoerceSelection();
        Rebuild();
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

    /// <summary>Synchronizes the visual parts (content host, tab strip, title bar) with <see cref="Items"/>.</summary>
    private void Rebuild()
    {
        if (contentHost is not null)
        {
            for (var i = contentHost.Children.Count - 1; i >= 0; i--)
            {
                if (contentHost.Children[i] is ToolWindow window && !items.Contains(window))
                {
                    contentHost.Children.RemoveAt(i);
                }
            }

            foreach (var window in items)
            {
                if (!contentHost.Children.Contains(window))
                {
                    contentHost.Children.Add(window);
                }
            }
        }

        if (tabStrip is not null)
        {
            tabStrip.Children.Clear();
            if (items.Count > 1)
            {
                foreach (var window in items)
                {
                    tabStrip.Children.Add(new ToolWindowTabItem { Window = window });
                }

                tabStrip.Visibility = Visibility.Visible;
            }
            else
            {
                tabStrip.Visibility = Visibility.Collapsed;
            }
        }

        ApplySelection();
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
