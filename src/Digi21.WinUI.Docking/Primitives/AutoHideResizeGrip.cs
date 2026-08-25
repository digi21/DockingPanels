using Digi21.WinUI.Docking.Interaction;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// The draggable inner edge of an open <see cref="AutoHideFlyout"/>. Dragging it resizes the panel
/// that is out, the way a <see cref="DockSplitter"/> resizes a docked pane, and leaves it
/// auto-hidden: the panel a user opened to read something too wide for it can be widened without
/// being pinned back into the layout.
/// </summary>
/// <remarks>
/// The new size is the auto-hide group's, so it outlives this visit to the panel and is saved with
/// the layout.
/// </remarks>
public partial class AutoHideResizeGrip : Control
{
    private DockSide edge = DockSide.Left;
    private Pointer? capturedPointer;

    // What the drag is measured against. The flyout is the thing being resized, so its own
    // coordinates move under the pointer; the dock site's do not.
    private UIElement? reference;
    private double startPosition;
    private double startSize;

    /// <summary>Initializes a new instance of the <see cref="AutoHideResizeGrip"/> class.</summary>
    public AutoHideResizeGrip()
    {
        DefaultStyleKey = typeof(AutoHideResizeGrip);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
    }

    // The edge the panel came out of, which decides both the cursor and which way a drag grows it.
    // Set by the flyout, which is told by the dock site every time the panel is placed.
    internal DockSide Edge
    {
        get => edge;
        set
        {
            if (edge == value)
            {
                return;
            }

            edge = value;
            UpdateVisuals();
        }
    }

    private bool IsDragging => capturedPointer is not null;

    private bool IsHorizontal => edge is DockSide.Left or DockSide.Right;

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateVisuals();
        VisualStateManager.GoToState(this, "Normal", false);
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        VisualStateManager.GoToState(this, IsDragging ? "Pressed" : "PointerOver", true);
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        VisualStateManager.GoToState(this, IsDragging ? "Pressed" : "Normal", true);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (this.FindAncestor<AutoHideFlyout>() is not { } flyout
            || flyout.Site is not { } site
            || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (!CapturePointer(e.Pointer))
        {
            return;
        }

        var position = e.GetCurrentPoint(site).Position;

        capturedPointer = e.Pointer;
        reference = site;
        startPosition = IsHorizontal ? position.X : position.Y;

        // The size on screen and not the group's, which a narrow window may be capping: the drag
        // must start from where the user sees the edge.
        startSize = IsHorizontal ? flyout.ActualWidth : flyout.ActualHeight;

        flyout.BeginResize();
        VisualStateManager.GoToState(this, "Pressed", true);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!IsDragging || reference is null || this.FindAncestor<AutoHideFlyout>() is not { } flyout)
        {
            return;
        }

        var position = e.GetCurrentPoint(reference).Position;
        var delta = (IsHorizontal ? position.X : position.Y) - startPosition;

        flyout.Resize(AutoHideResizePolicy.SizeFromDrag(edge, startSize, delta));
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (IsDragging)
        {
            ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerRoutedEventArgs e)
    {
        base.OnPointerCaptureLost(e);

        // Also the end of a drag released normally: releasing the capture is what raises this. The
        // layout change is announced once here rather than on every frame of the drag.
        var wasDragging = IsDragging;

        capturedPointer = null;
        reference = null;
        VisualStateManager.GoToState(this, "Normal", true);

        if (wasDragging)
        {
            this.FindAncestor<AutoHideFlyout>()?.EndResize();
        }
    }

    private void UpdateVisuals()
    {
        ProtectedCursor = InputSystemCursor.Create(
            IsHorizontal ? InputSystemCursorShape.SizeWestEast : InputSystemCursorShape.SizeNorthSouth);

        if (GetTemplateChild("GripLine") is Rectangle grip)
        {
            // Which axis the line runs along depends on the edge, so its thickness cannot come from
            // the template; it is read from the resources instead of being fixed here.
            var thickness = DockingThemeResources.Value("DockingSplitterGripThickness", 1.0);

            if (IsHorizontal)
            {
                grip.Width = thickness;
                grip.Height = double.NaN;
                grip.HorizontalAlignment = HorizontalAlignment.Center;
                grip.VerticalAlignment = VerticalAlignment.Stretch;
            }
            else
            {
                grip.Width = double.NaN;
                grip.Height = thickness;
                grip.HorizontalAlignment = HorizontalAlignment.Stretch;
                grip.VerticalAlignment = VerticalAlignment.Center;
            }
        }
    }
}
