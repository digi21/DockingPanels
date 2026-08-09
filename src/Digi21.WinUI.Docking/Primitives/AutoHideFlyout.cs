using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking.Primitives;

/// <summary>
/// The panel that slides over the workspace when an auto-hide tab is clicked, hosting the
/// window's content together with a title bar whose pin button re-docks the group. It is
/// hosted in a popup by the <see cref="DockSite"/> template: rendering it as a sibling of
/// the main layout suppresses text rendering in that subtree on some WinUI versions.
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

    // Shows the given auto-hidden window with the given explicit size.
    internal void Show(ToolWindow newWindow, double width, double height)
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

        Width = width;
        Height = height;
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
