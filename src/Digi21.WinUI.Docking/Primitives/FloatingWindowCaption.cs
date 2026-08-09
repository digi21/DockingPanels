using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// The caption of a floating window that hosts more than one pane. Dragging it moves the whole
/// floating window and can dock it back, exactly as dragging the title bar of a floating window
/// with a single pane does.
/// </summary>
/// <remarks>
/// A floating window has no system title bar: with a single pane the pane's own title bar acts as
/// the caption, but once windows are docked inside it, that title bar belongs to one pane and can
/// no longer stand for the whole window, so this caption takes over.
/// </remarks>
public partial class FloatingWindowCaption : Control
{
    /// <summary>Identifies the <see cref="Title"/> dependency property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(FloatingWindowCaption),
        new PropertyMetadata(string.Empty, (d, _) => ((FloatingWindowCaption)d).UpdateTitle()));

    private TextBlock? titleText;
    private Button? closeButton;

    /// <summary>Initializes a new instance of the <see cref="FloatingWindowCaption"/> class.</summary>
    public FloatingWindowCaption()
    {
        DefaultStyleKey = typeof(FloatingWindowCaption);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
    }

    /// <summary>Gets or sets the text shown in the caption.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the floating window this caption belongs to.</summary>
    internal FloatingWindowHost? Host { get; set; }

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

        UpdateTitle();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Host is { } host)
        {
            host.Site.DragController?.BeginHostDrag(host, this, e);
        }
    }

    /// <inheritdoc />
    protected override void OnDoubleTapped(DoubleTappedRoutedEventArgs e)
    {
        base.OnDoubleTapped(e);

        // Same gesture as a single-pane floating window: the whole group goes back to the layout.
        Host?.DockHome();
        e.Handled = true;
    }

    private void UpdateTitle()
    {
        if (titleText is not null)
        {
            titleText.Text = Title;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Host?.CloseHostedWindows();
    }
}
