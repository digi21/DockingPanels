using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Digi21.WinUI.Docking;

internal static class VisualTreeExtensions
{
    // Walks up the visual tree looking for the nearest ancestor of type T.
    internal static T? FindAncestor<T>(this DependencyObject start)
        where T : DependencyObject
    {
        var current = VisualTreeHelper.GetParent(start);
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    // Walks up the visual tree looking for the docking surface the element lives in: the dock site,
    // or the floating window hosting it.
    internal static IDockSurface? FindSurface(this DependencyObject start)
    {
        var current = VisualTreeHelper.GetParent(start);
        while (current is not null)
        {
            if (current is IDockSurface surface)
            {
                return surface;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    // Walks up the visual tree looking for the element that owns the layout tree the given element
    // belongs to: the nearest document host, or the surface it lives in.
    internal static ILayoutHost? FindLayoutHost(this DependencyObject start)
    {
        var current = VisualTreeHelper.GetParent(start);
        while (current is not null)
        {
            if (current is ILayoutHost host)
            {
                return host;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
