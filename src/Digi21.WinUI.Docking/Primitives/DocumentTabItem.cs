using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// A tab shown along the top of a <see cref="DocumentContainer"/> for one of its documents.
/// Clicking it selects and activates the document, dragging it moves the document to another
/// tab position, group or window, its pin button pins it to the head of the strip, and its close
/// button closes it.
/// </summary>
public partial class DocumentTabItem : Control
{
    /// <summary>Identifies the <see cref="Window"/> dependency property.</summary>
    public static readonly DependencyProperty WindowProperty = DependencyProperty.Register(
        nameof(Window),
        typeof(DockingWindow),
        typeof(DocumentTabItem),
        new PropertyMetadata(null, (d, _) => ((DocumentTabItem)d).OnWindowChanged()));

    private readonly MenuFlyout contextMenu = new();
    private DockingWindow? observed;
    private long titleToken = -1;
    private long isSelectedToken = -1;
    private long isActiveToken = -1;
    private long canCloseToken = -1;
    private long isPinnedToken = -1;
    private TextBlock? titleText;
    private Button? pinButton;
    private Button? closeButton;
    private bool pointerOver;

    /// <summary>Initializes a new instance of the <see cref="DocumentTabItem"/> class.</summary>
    public DocumentTabItem()
    {
        DefaultStyleKey = typeof(DocumentTabItem);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");

        contextMenu.Opening += OnContextMenuOpening;
        ContextFlyout = contextMenu;
    }

    /// <summary>Gets or sets the document this tab represents.</summary>
    public DockingWindow? Window
    {
        get => (DockingWindow?)GetValue(WindowProperty);
        set => SetValue(WindowProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (closeButton is not null)
        {
            closeButton.Click -= OnCloseClick;
        }

        if (pinButton is not null)
        {
            pinButton.Click -= OnPinClick;
        }

        titleText = GetTemplateChild("PART_Title") as TextBlock;
        pinButton = GetTemplateChild("PART_PinTabButton") as Button;
        closeButton = GetTemplateChild("PART_CloseButton") as Button;

        if (closeButton is not null)
        {
            closeButton.Click += OnCloseClick;
        }

        if (pinButton is not null)
        {
            pinButton.Click += OnPinClick;
        }

        Update();
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        pointerOver = true;
        UpdateCommonState();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        pointerOver = false;
        UpdateCommonState();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        VisualStateManager.GoToState(this, "Pressed", true);
        Window?.Activate();

        if (Window is { } window && window.DockSite?.DragController is { } controller)
        {
            controller.BeginPotentialDrag(window, this, e);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        UpdateCommonState();
    }

    private void OnWindowChanged()
    {
        if (observed is not null)
        {
            observed.UnregisterPropertyChangedCallback(DockingWindow.TitleProperty, titleToken);
            observed.UnregisterPropertyChangedCallback(DockingWindow.IsSelectedProperty, isSelectedToken);
            observed.UnregisterPropertyChangedCallback(DockingWindow.IsActiveProperty, isActiveToken);
            observed.UnregisterPropertyChangedCallback(DockingWindow.CanCloseProperty, canCloseToken);

            if (observed is DocumentWindow previous)
            {
                previous.UnregisterPropertyChangedCallback(DocumentWindow.IsPinnedProperty, isPinnedToken);
            }
        }

        observed = Window;

        if (observed is not null)
        {
            titleToken = observed.RegisterPropertyChangedCallback(DockingWindow.TitleProperty, (_, _) => Update());
            isSelectedToken = observed.RegisterPropertyChangedCallback(DockingWindow.IsSelectedProperty, (_, _) => Update());
            isActiveToken = observed.RegisterPropertyChangedCallback(DockingWindow.IsActiveProperty, (_, _) => Update());
            canCloseToken = observed.RegisterPropertyChangedCallback(DockingWindow.CanCloseProperty, (_, _) => Update());

            if (observed is DocumentWindow document)
            {
                isPinnedToken = document.RegisterPropertyChangedCallback(DocumentWindow.IsPinnedProperty, (_, _) => Update());
            }
        }

        Update();
    }

    private void Update()
    {
        var title = observed?.Title ?? string.Empty;

        if (titleText is not null)
        {
            titleText.Text = title;
        }

        AutomationProperties.SetName(this, title);

        if (closeButton is not null)
        {
            closeButton.Visibility = observed?.CanClose == true ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdatePinButton();

        VisualStateManager.GoToState(
            this,
            observed switch
            {
                { IsSelected: true, IsActive: true } => "Active",
                { IsSelected: true } => "Selected",
                _ => "Unselected",
            },
            true);

        UpdateCommonState();
    }

    // Brings the pin button in line with the document: it offers to pin an ordinary tab and to unpin
    // a pinned one, and a pinned tab shows it at all times, since that is what says the tab is
    // pinned when the pointer is elsewhere.
    private void UpdatePinButton()
    {
        if (pinButton is null)
        {
            return;
        }

        if (observed is not DocumentWindow document)
        {
            pinButton.Visibility = Visibility.Collapsed;
            return;
        }

        pinButton.Visibility = Visibility.Visible;

        if (pinButton.Content is FontIcon icon)
        {
            icon.Glyph = document.IsPinned
                ? DockingThemeResources.Value("DockingTabUnpinGlyph", "\uE77A")
                : DockingThemeResources.Value("DockingTabPinGlyph", "\uE718");
        }

        var name = PinCommandName(document);
        AutomationProperties.SetName(pinButton, name);
        ToolTipService.SetToolTip(pinButton, name);
    }

    private static string PinCommandName(DocumentWindow document)
    {
        return document.IsPinned
            ? DockingThemeResources.Value("DockingUnpinTabButtonName", "Unpin tab")
            : DockingThemeResources.Value("DockingPinTabButtonName", "Pin tab");
    }

    private void UpdateCommonState()
    {
        VisualStateManager.GoToState(this, pointerOver ? "PointerOver" : "Normal", true);

        if (pinButton is not null)
        {
            var pinned = observed is DocumentWindow { IsPinned: true };
            pinButton.Opacity = pinned || pointerOver || observed?.IsSelected == true ? 1 : 0;
        }
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        if (observed is DocumentWindow document)
        {
            document.IsPinned = !document.IsPinned;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Window?.Close();
    }

    // Fills the tab's context menu just before it opens, then lets the application have the list:
    // whatever it leaves there is what the menu shows.
    private void OnContextMenuOpening(object? sender, object e)
    {
        contextMenu.Items.Clear();

        if (observed is not DocumentWindow document)
        {
            return;
        }

        var host = this.FindLayoutHost() as DocumentHost;
        var items = new List<MenuFlyoutItemBase>
        {
            Command(PinCommandName(document), () => document.IsPinned = !document.IsPinned),
        };

        if (document.CanClose)
        {
            items.Add(new MenuFlyoutSeparator());
            items.Add(Command(
                DockingThemeResources.Value("DockingCloseButtonName", "Close"),
                document.Close));
        }

        if (host is not null)
        {
            if (items.Count == 1)
            {
                items.Add(new MenuFlyoutSeparator());
            }

            items.Add(Command(
                DockingThemeResources.Value("DockingCloseAllTabsName", "Close all tabs"),
                () => host.CloseDocuments(DocumentCloseScope.All)));
            items.Add(Command(
                DockingThemeResources.Value("DockingCloseAllButPinnedTabsName", "Close all but pinned"),
                () => host.CloseDocuments(DocumentCloseScope.AllButPinned)));
            items.Add(Command(
                DockingThemeResources.Value("DockingCloseAllButThisTabName", "Close all but this"),
                () =>
                {
                    // "This" is the tab the menu was opened on, which right-clicking it has already
                    // made the active document.
                    document.Activate();
                    host.CloseDocuments(DocumentCloseScope.AllButActive);
                }));
        }

        document.DockSite?.RaiseDocumentTabContextMenuOpening(document, items);

        foreach (var item in items)
        {
            contextMenu.Items.Add(item);
        }
    }

    private static MenuFlyoutItem Command(string text, Action execute)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += (_, _) => execute();
        return item;
    }
}
