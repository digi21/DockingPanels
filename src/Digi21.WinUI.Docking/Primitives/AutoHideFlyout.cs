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
    private Grid? contentHost;
    private ToolWindowTitleBar? titleBar;

    /// <summary>Initializes a new instance of the <see cref="AutoHideFlyout"/> class.</summary>
    public AutoHideFlyout()
    {
        DefaultStyleKey = typeof(AutoHideFlyout);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
    }

    /// <summary>Gets the window currently shown by the flyout, if any.</summary>
    internal ToolWindow? Window => window;

    /// <inheritdoc />
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        contentHost = GetTemplateChild("PART_ContentHost") as Grid;
        titleBar = GetTemplateChild("PART_TitleBar") as ToolWindowTitleBar;
    }

    /// <summary>Shows the given auto-hidden window with the given explicit size.</summary>
    internal void Show(ToolWindow newWindow, double width, double height)
    {
        ApplyTemplate();
        Release();

        window = newWindow;
        window.Visibility = Visibility.Visible;
        contentHost?.Children.Add(window);

        if (titleBar is not null)
        {
            titleBar.Window = window;
        }

        Width = width;
        Height = height;
    }

    /// <summary>Releases the hosted window so it can be re-docked or shown elsewhere.</summary>
    internal void Release()
    {
        if (window is not null)
        {
            contentHost?.Children.Remove(window);
            if (titleBar is not null)
            {
                titleBar.Window = null;
            }

            window = null;
        }
    }
}
