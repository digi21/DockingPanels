using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

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

    // Releases the hosted window so it can be re-docked or shown elsewhere.
    internal void Release()
    {
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
