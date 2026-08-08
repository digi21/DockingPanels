using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// The title bar shown at the top of a <see cref="ToolWindowContainer"/> for its selected window.
/// Shows the window title, reflects the active state, and hosts the close button.
/// </summary>
public partial class ToolWindowTitleBar : Control
{
    /// <summary>Identifies the <see cref="Window"/> dependency property.</summary>
    public static readonly DependencyProperty WindowProperty = DependencyProperty.Register(
        nameof(Window),
        typeof(DockingWindow),
        typeof(ToolWindowTitleBar),
        new PropertyMetadata(null, (d, _) => ((ToolWindowTitleBar)d).OnWindowChanged()));

    private DockingWindow? observed;
    private long titleToken = -1;
    private long isActiveToken = -1;
    private long canCloseToken = -1;
    private TextBlock? titleText;
    private Button? closeButton;

    /// <summary>Initializes a new instance of the <see cref="ToolWindowTitleBar"/> class.</summary>
    public ToolWindowTitleBar()
    {
        DefaultStyleKey = typeof(ToolWindowTitleBar);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
    }

    /// <summary>Gets or sets the docking window this title bar represents.</summary>
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
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        Window?.Activate();

        if (Window is ToolWindow tool && tool.DockSite?.DragController is { } controller)
        {
            controller.BeginPotentialDrag(tool, this, e);
        }
    }

    private void OnWindowChanged()
    {
        if (observed is not null)
        {
            observed.UnregisterPropertyChangedCallback(DockingWindow.TitleProperty, titleToken);
            observed.UnregisterPropertyChangedCallback(DockingWindow.IsActiveProperty, isActiveToken);
            observed.UnregisterPropertyChangedCallback(DockingWindow.CanCloseProperty, canCloseToken);
        }

        observed = Window;

        if (observed is not null)
        {
            titleToken = observed.RegisterPropertyChangedCallback(DockingWindow.TitleProperty, (_, _) => Update());
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

        VisualStateManager.GoToState(this, observed?.IsActive == true ? "Active" : "Inactive", true);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Window?.Close();
    }
}
