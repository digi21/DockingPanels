using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// The panel that slides over the workspace when an auto-hide tab is pointed at or clicked,
/// hosting the window's content together with a title bar whose pin button re-docks the group.
/// The <see cref="DockSite"/> template puts it on the overlay canvas rather than in a popup, so
/// it shares the dock site's focus scope: the focus moving in and out of it is what tells the
/// dock site whether the panel is still in use.
/// </summary>
public partial class AutoHideFlyout : Control
{
    private ToolWindow? window;
    private Storyboard? slide;
    private long slideGeneration;

    // Owned host for the shown window; survives template re-application (see ToolWindowContainer).
    private readonly Grid windowHost = new();
    private ContentPresenter? contentSlot;
    private ToolWindowTitleBar? titleBar;

    /// <summary>Initializes a new instance of the <see cref="AutoHideFlyout"/> class.</summary>
    public AutoHideFlyout()
    {
        DefaultStyleKey = typeof(AutoHideFlyout);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
    }

    // Gets the window currently shown by the flyout, if any.
    internal ToolWindow? Window => window;

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);

        // Bubbles from the content as well, so the pointer moving between the panel's own controls
        // keeps calling off a collapse that the crossing from the tab may have scheduled.
        this.FindAncestor<DockSite>()?.NotifyAutoHidePointerEntered();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        this.FindAncestor<DockSite>()?.NotifyAutoHidePointerExited();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (contentSlot is not null)
        {
            contentSlot.Content = null;
        }

        contentSlot = GetTemplateChild("PART_ContentHost") as ContentPresenter;
        if (contentSlot is not null)
        {
            contentSlot.Content = windowHost;
        }

        titleBar = GetTemplateChild("PART_TitleBar") as ToolWindowTitleBar;
    }

    // Shows the given auto-hidden window. The dock site sizes and places the flyout, and does it
    // again whenever the area it slides over changes size.
    internal void Show(ToolWindow newWindow)
    {
        ApplyTemplate();
        Release();

        window = newWindow;
        window.Visibility = Visibility.Visible;
        windowHost.Children.Add(window);

        if (titleBar is not null)
        {
            titleBar.Window = window;
        }
    }

    // Slides the panel out from its edge, the way it does in Visual Studio, over the given
    // duration.
    //
    // What moves is the template root and not the flyout: the flyout stays where the dock site put
    // it and clips to its own bounds, so the panel appears to come out from under the edge instead
    // of sweeping across the window and over whatever else the application draws around the dock
    // site. Restoring the clip afterwards matters — a panel wider than its own bounds does not
    // exist, but a shadow or a focus ring drawn outside them does.
    //
    // A transform and not Width: the window inside is a live panel, and animating its size would
    // re-measure and re-arrange the application's content on every frame. A render transform is
    // composited and touches no layout, so the content is laid out once, at its final size, and is
    // in the automation tree from the first frame rather than at the end of the animation.
    internal void SlideIn(DockSide edge, TimeSpan duration)
    {
        Slide(edge, duration, arriving: true, onCompleted: null);
    }

    // Slides the panel back into its edge and calls onCompleted once it is out of sight, which is
    // where the caller puts the window away for real.
    //
    // Returns false when there is nothing to animate, and then the callback is not called either:
    // the caller does its own work at once, which is the same thing it does when the animation is
    // turned off. The callback of a slide that is cancelled — see CancelSlide — never runs at all,
    // since cancelling means something else has taken over the flyout.
    internal bool SlideOut(DockSide edge, TimeSpan duration, Action onCompleted)
    {
        return Slide(edge, duration, arriving: false, onCompleted);
    }

    // Stops a slide in progress and puts the panel back at rest where it belongs, unclipped and
    // untransformed. Anything that needs the flyout right now calls this first: the slide is
    // abandoned where it is rather than played out, and its callback is dropped.
    internal void CancelSlide()
    {
        slideGeneration++;
        slide?.Stop();
        slide = null;

        if (GetTemplateRoot() is FrameworkElement root)
        {
            root.RenderTransform = null;
        }

        Clip = null;
    }

    private bool Slide(DockSide edge, TimeSpan duration, bool arriving, Action? onCompleted)
    {
        CancelSlide();

        if (GetTemplateRoot() is not FrameworkElement root)
        {
            return false;
        }

        var vertical = edge is DockSide.Top or DockSide.Bottom;
        var distance = vertical ? Height : Width;
        if (!double.IsFinite(distance) || distance <= 0)
        {
            // Nothing to slide across: the dock site sizes the flyout before showing it, so this is
            // a flyout that is not on screen at all.
            return false;
        }

        var offset = edge is DockSide.Left or DockSide.Top ? -distance : distance;
        var (from, to) = arriving ? (offset, 0.0) : (0.0, offset);

        var transform = new TranslateTransform();
        root.RenderTransform = transform;
        Clip = new RectangleGeometry { Rect = new Rect(0, 0, Width, Height) };

        var animation = new DoubleAnimationUsingKeyFrames();
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame { KeyTime = TimeSpan.Zero, Value = from });
        animation.KeyFrames.Add(new SplineDoubleKeyFrame
        {
            KeyTime = duration,
            Value = to,

            // The curves Fluent uses: something arriving is quick to start and settles at the end,
            // something leaving does the opposite. Taken from the system rather than invented, so
            // the panel moves like the rest of the shell.
            KeySpline = arriving
                ? new KeySpline { ControlPoint1 = new Point(0.1, 0.9), ControlPoint2 = new Point(0.2, 1.0) }
                : new KeySpline { ControlPoint1 = new Point(0.7, 0.0), ControlPoint2 = new Point(1.0, 0.5) },
        });

        Storyboard.SetTarget(animation, transform);
        Storyboard.SetTargetProperty(animation, vertical ? "Y" : "X");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);

        // The generation is what tells a natural end from a stale one: a stopped storyboard is not
        // guaranteed to stay quiet, and a callback that ran after something else had taken the
        // flyout over would put away a panel that is on screen again.
        var generation = slideGeneration;
        storyboard.Completed += (_, _) =>
        {
            if (generation != slideGeneration)
            {
                return;
            }

            CancelSlide();
            onCompleted?.Invoke();
        };

        slide = storyboard;
        storyboard.Begin();
        return true;
    }

    private UIElement? GetTemplateRoot()
    {
        return VisualTreeHelper.GetChildrenCount(this) > 0 ? VisualTreeHelper.GetChild(this, 0) as UIElement : null;
    }

    // Releases the hosted window so it can be re-docked or shown elsewhere.
    internal void Release()
    {
        CancelSlide();

        if (window is not null)
        {
            windowHost.Children.Remove(window);
            if (titleBar is not null)
            {
                titleBar.Window = null;
            }

            window = null;
        }
    }
}
