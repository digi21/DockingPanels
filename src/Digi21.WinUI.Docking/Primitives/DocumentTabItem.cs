using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// A tab shown along the top of a <see cref="DocumentContainer"/> for one of its documents.
/// Clicking it selects and activates the document, dragging it moves the document to another
/// tab position, group or window, and its close button closes it.
/// </summary>
public partial class DocumentTabItem : Control
{
    /// <summary>Identifies the <see cref="Window"/> dependency property.</summary>
    public static readonly DependencyProperty WindowProperty = DependencyProperty.Register(
        nameof(Window),
        typeof(DockingWindow),
        typeof(DocumentTabItem),
        new PropertyMetadata(null, (d, _) => ((DocumentTabItem)d).OnWindowChanged()));

    private DockingWindow? observed;
    private long titleToken = -1;
    private long isSelectedToken = -1;
    private long isActiveToken = -1;
    private long canCloseToken = -1;
    private TextBlock? titleText;
    private Button? closeButton;
    private bool pointerOver;

    /// <summary>Initializes a new instance of the <see cref="DocumentTabItem"/> class.</summary>
    public DocumentTabItem()
    {
        DefaultStyleKey = typeof(DocumentTabItem);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
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

        titleText = GetTemplateChild("PART_Title") as TextBlock;
        closeButton = GetTemplateChild("PART_CloseButton") as Button;

        if (closeButton is not null)
        {
            closeButton.Click += OnCloseClick;
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
        }

        observed = Window;

        if (observed is not null)
        {
            titleToken = observed.RegisterPropertyChangedCallback(DockingWindow.TitleProperty, (_, _) => Update());
            isSelectedToken = observed.RegisterPropertyChangedCallback(DockingWindow.IsSelectedProperty, (_, _) => Update());
            isActiveToken = observed.RegisterPropertyChangedCallback(DockingWindow.IsActiveProperty, (_, _) => Update());
            canCloseToken = observed.RegisterPropertyChangedCallback(DockingWindow.CanCloseProperty, (_, _) => Update());
        }

        Update();
    }

    private void Update()
    {
        if (titleText is not null)
        {
            titleText.Text = observed?.Title ?? string.Empty;
        }

        if (closeButton is not null)
        {
            closeButton.Visibility = observed?.CanClose == true ? Visibility.Visible : Visibility.Collapsed;
        }

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

    private void UpdateCommonState()
    {
        VisualStateManager.GoToState(this, pointerOver ? "PointerOver" : "Normal", true);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Window?.Close();
    }
}
