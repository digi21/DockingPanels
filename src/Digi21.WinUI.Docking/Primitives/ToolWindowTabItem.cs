using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// A tab shown at the bottom of a <see cref="ToolWindowContainer"/> when it hosts multiple
/// tool windows. Clicking the tab selects and activates its window.
/// </summary>
public partial class ToolWindowTabItem : Control
{
    /// <summary>Identifies the <see cref="Window"/> dependency property.</summary>
    public static readonly DependencyProperty WindowProperty = DependencyProperty.Register(
        nameof(Window),
        typeof(ToolWindow),
        typeof(ToolWindowTabItem),
        new PropertyMetadata(null, (d, _) => ((ToolWindowTabItem)d).OnWindowChanged()));

    private ToolWindow? observed;
    private long titleToken = -1;
    private long isSelectedToken = -1;
    private TextBlock? titleText;
    private bool pointerOver;

    /// <summary>Initializes a new instance of the <see cref="ToolWindowTabItem"/> class.</summary>
    public ToolWindowTabItem()
    {
        DefaultStyleKey = typeof(ToolWindowTabItem);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
    }

    /// <summary>Gets or sets the tool window this tab represents.</summary>
    public ToolWindow? Window
    {
        get => (ToolWindow?)GetValue(WindowProperty);
        set => SetValue(WindowProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        titleText = GetTemplateChild("PART_Title") as TextBlock;
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
        }

        observed = Window;

        if (observed is not null)
        {
            titleToken = observed.RegisterPropertyChangedCallback(DockingWindow.TitleProperty, (_, _) => Update());
            isSelectedToken = observed.RegisterPropertyChangedCallback(DockingWindow.IsSelectedProperty, (_, _) => Update());
        }

        Update();
    }

    private void Update()
    {
        if (titleText is not null)
        {
            titleText.Text = observed?.Title ?? string.Empty;
        }

        VisualStateManager.GoToState(this, observed?.IsSelected == true ? "Selected" : "Unselected", true);
        UpdateCommonState();
    }

    private void UpdateCommonState()
    {
        VisualStateManager.GoToState(this, pointerOver ? "PointerOver" : "Normal", true);
    }
}
