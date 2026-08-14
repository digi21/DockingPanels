using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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
    private DockingWindowContainer? observedContainer;
    private long titleToken = -1;
    private long isActiveToken = -1;
    private long canCloseToken = -1;
    private long canAutoHideToken = -1;
    private long stateToken = -1;
    private TextBlock? titleText;
    private Button? closeButton;
    private Button? pinButton;

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

        if (pinButton is not null)
        {
            pinButton.Click -= OnPinClick;
        }

        titleText = GetTemplateChild("PART_Title") as TextBlock;
        closeButton = GetTemplateChild("PART_CloseButton") as Button;
        pinButton = GetTemplateChild("PART_PinButton") as Button;

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
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        Window?.Activate();

        if (Window is ToolWindow tool && tool.DockSite?.DragController is { } controller)
        {
            controller.BeginPotentialDrag(tool, this, e);
        }
    }

    /// <inheritdoc />
    protected override void OnDoubleTapped(DoubleTappedRoutedEventArgs e)
    {
        base.OnDoubleTapped(e);

        // Same gesture as Visual Studio: a docked window floats, a floating one goes back home.
        if (Window is ToolWindow tool)
        {
            switch (tool.State)
            {
                case DockingWindowState.Docked:
                    tool.Float();
                    break;
                case DockingWindowState.Floating:
                    tool.Dock();
                    break;
            }

            e.Handled = true;
        }
    }

    private void OnWindowChanged()
    {
        if (observed is not null)
        {
            observed.UnregisterPropertyChangedCallback(DockingWindow.TitleProperty, titleToken);
            observed.UnregisterPropertyChangedCallback(DockingWindow.IsActiveProperty, isActiveToken);
            observed.UnregisterPropertyChangedCallback(DockingWindow.CanCloseProperty, canCloseToken);
            observed.UnregisterPropertyChangedCallback(DockingWindow.CanAutoHideProperty, canAutoHideToken);
            observed.UnregisterPropertyChangedCallback(DockingWindow.StateProperty, stateToken);
        }

        if (observedContainer is not null)
        {
            observedContainer.ItemsChanged -= OnContainerItemsChanged;
            observedContainer = null;
        }

        observed = Window;

        // Dragging a window that cannot auto-hide into this tab group takes the pin away from the
        // windows already in it, so the group's membership is watched as closely as the window's
        // own properties.
        observedContainer = observed?.Container;
        if (observedContainer is not null)
        {
            observedContainer.ItemsChanged += OnContainerItemsChanged;
        }

        if (observed is not null)
        {
            titleToken = observed.RegisterPropertyChangedCallback(DockingWindow.TitleProperty, (_, _) => Update());
            isActiveToken = observed.RegisterPropertyChangedCallback(DockingWindow.IsActiveProperty, (_, _) => Update());
            canCloseToken = observed.RegisterPropertyChangedCallback(DockingWindow.CanCloseProperty, (_, _) => Update());
            canAutoHideToken = observed.RegisterPropertyChangedCallback(DockingWindow.CanAutoHideProperty, (_, _) => Update());
            stateToken = observed.RegisterPropertyChangedCallback(DockingWindow.StateProperty, (_, _) => Update());
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

        if (pinButton is not null)
        {
            // The container matters as much as the window: unpinning takes the whole tab group to
            // the edge, so a group holding one window that must stay docked shows no pin at all
            // rather than a button that does nothing.
            var canPin = observed is ToolWindow { CanAutoHide: true }
                && observed.State is DockingWindowState.Docked or DockingWindowState.AutoHide
                && (observed.Container is not { } container || LayoutManager.CanAutoHideContainer(container));
            pinButton.Visibility = canPin ? Visibility.Visible : Visibility.Collapsed;

            // Docked: vertical pin, click auto-hides. Auto-hidden: unpin glyph, click docks. The
            // button says which of the two it is doing, because one button doing opposite things
            // is otherwise unreadable to anything that cannot see the glyph \u2014 a screen reader, or
            // an automated test looking for it by name.
            var unpinned = observed?.State == DockingWindowState.AutoHide;

            if (pinButton.Content is FontIcon icon)
            {
                icon.Glyph = unpinned
                    ? DockingThemeResources.Value("DockingUnpinGlyph", "\uE77A")
                    : DockingThemeResources.Value("DockingPinGlyph", "\uE718");
            }

            var pinName = unpinned
                ? DockingThemeResources.Value("DockingDockButtonName", "Dock")
                : DockingThemeResources.Value("DockingAutoHideButtonName", "Auto-hide");
            AutomationProperties.SetName(pinButton, pinName);
            ToolTipService.SetToolTip(pinButton, pinName);
        }

        VisualStateManager.GoToState(this, observed?.IsActive == true ? "Active" : "Inactive", true);
    }

    private void OnContainerItemsChanged(object? sender, EventArgs e)
    {
        Update();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Window?.Close();
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        if (Window is ToolWindow tool)
        {
            if (tool.State == DockingWindowState.AutoHide)
            {
                tool.Dock();
            }
            else
            {
                tool.AutoHide();
            }
        }
    }
}
