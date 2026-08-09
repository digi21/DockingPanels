using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// The root control of a floating window: it hosts the window's layout tree, its caption and its
/// dock guides, which makes a floating window a docking surface in its own right.
/// </summary>
/// <remarks>
/// Its template mirrors the part of the <see cref="DockSite"/> template that takes part in
/// dragging (preview, guide cluster, edge guides and drag ghost), so a drop inside a floating
/// window looks and behaves exactly like a drop on the dock site.
/// </remarks>
public partial class FloatingWindowRoot : Control, IDockSurface
{
    /// <summary>Identifies the <see cref="Child"/> dependency property.</summary>
    public static readonly DependencyProperty ChildProperty = DependencyProperty.Register(
        nameof(Child),
        typeof(UIElement),
        typeof(FloatingWindowRoot),
        new PropertyMetadata(null));

    private readonly DockSite site;
    private FloatingWindowCaption? caption;
    private string captionTitle = string.Empty;
    private bool captionVisible;

    // Initializes a new instance of the FloatingWindowRoot class.
    //
    // site: The dock site the floating window belongs to.
    //
    // host: The floating window hosting this root.
    internal FloatingWindowRoot(DockSite site, FloatingWindowHost host)
    {
        this.site = site;
        Host = host;
        DefaultStyleKey = typeof(FloatingWindowRoot);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
    }

    /// <summary>Gets or sets the root element of the floating window's layout tree.</summary>
    public UIElement? Child
    {
        get => (UIElement?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    // Gets the floating window this root belongs to.
    internal FloatingWindowHost Host { get; }

    // Gets the guide overlay, or null until the template is applied.
    internal DockGuideOverlay? Overlay { get; private set; }

    // Shows or hides the caption, and sets its text.
    internal void SetCaption(string title, bool visible)
    {
        captionTitle = title;
        captionVisible = visible;
        ApplyCaption();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        caption = GetTemplateChild("PART_Caption") as FloatingWindowCaption;
        if (caption is not null)
        {
            caption.Host = Host;
        }

        ApplyCaption();

        if (GetTemplateChild("PART_DockPreview") is Rectangle preview
            && GetTemplateChild("PART_CenterGuides") is DockGuidePanel centerGuides
            && GetTemplateChild("PART_EdgeGuideLeft") is DockGuide left
            && GetTemplateChild("PART_EdgeGuideTop") is DockGuide top
            && GetTemplateChild("PART_EdgeGuideRight") is DockGuide right
            && GetTemplateChild("PART_EdgeGuideBottom") is DockGuide bottom)
        {
            Overlay = new DockGuideOverlay(
                this,
                preview,
                GetTemplateChild("PART_DragGhost") as Border,
                GetTemplateChild("PART_DragGhostText") as TextBlock,
                centerGuides,
                new Dictionary<DockSide, DockGuide>
                {
                    [DockSide.Left] = left,
                    [DockSide.Top] = top,
                    [DockSide.Right] = right,
                    [DockSide.Bottom] = bottom,
                });
        }
        else
        {
            Overlay = null;
        }
    }

    private void ApplyCaption()
    {
        if (caption is not null)
        {
            caption.Title = captionTitle;
            caption.Visibility = captionVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    DockSite IDockSurface.Site => site;

    FrameworkElement IDockSurface.Root => this;

    bool IDockSurface.IsFloating => true;

    // Restructuring the tree detaches the old root before plugging the new one in, so an empty
    // tree here is a passing state, not an empty window: whether the window still holds anything
    // is settled once the mutation is over, in OnLayoutMutated.
    UIElement? ILayoutHost.LayoutChild
    {
        get => Child;
        set => Child = value;
    }

    DockGuideOverlay? IDockSurface.Overlay => Overlay;

    void IDockSurface.OnLayoutMutated() => Host.SyncWithLayout();
}
