using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Bridges XAML coordinates and screen coordinates. Floating windows are real top-level
/// windows, so dragging one over the dock site spans two XAML islands with unrelated
/// coordinate spaces: <c>TransformToVisual</c> cannot cross them and the pointer positions
/// reported by each island are relative to its own window. Screen pixels are the only common
/// reference, and the cursor position read from the system is the only reliable source while
/// the dragged window is being moved under the pointer.
/// </summary>
internal static class ScreenInterop
{
    /// <summary>Gets the current cursor position in screen pixels.</summary>
    internal static PointInt32 CursorPosition
    {
        get => GetCursorPos(out var point) ? new PointInt32(point.X, point.Y) : default;
    }

    /// <summary>
    /// Converts a point expressed in an element's own coordinates (device-independent pixels)
    /// into screen pixels, or returns <see langword="null"/> when the element is not connected
    /// to a window yet.
    /// </summary>
    internal static PointInt32? ToScreen(FrameworkElement element, Point local)
    {
        if (element.XamlRoot is not { } xamlRoot || GetWindowHandle(element) is not { } hwnd)
        {
            return null;
        }

        Point inWindow;
        try
        {
            inWindow = element.TransformToVisual(xamlRoot.Content).TransformPoint(local);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var origin = new NativePoint();
        if (!ClientToScreen(hwnd, ref origin))
        {
            return null;
        }

        var scale = xamlRoot.RasterizationScale;
        return new PointInt32(
            origin.X + (int)Math.Round(inWindow.X * scale),
            origin.Y + (int)Math.Round(inWindow.Y * scale));
    }

    /// <summary>
    /// Converts a screen point into an element's own coordinates (device-independent pixels),
    /// or returns <see langword="null"/> when the element is not connected to a window yet.
    /// </summary>
    internal static Point? FromScreen(FrameworkElement element, PointInt32 screen)
    {
        if (ToScreen(element, default) is not { } elementOrigin || element.XamlRoot is not { } xamlRoot)
        {
            return null;
        }

        var scale = xamlRoot.RasterizationScale;
        return new Point(
            (screen.X - elementOrigin.X) / scale,
            (screen.Y - elementOrigin.Y) / scale);
    }

    /// <summary>Converts a size in device-independent pixels to screen pixels for the given element's window.</summary>
    internal static SizeInt32 ToScreenSize(FrameworkElement element, double width, double height)
    {
        var scale = element.XamlRoot?.RasterizationScale ?? 1.0;
        return new SizeInt32((int)Math.Round(width * scale), (int)Math.Round(height * scale));
    }

    /// <summary>
    /// Gets the top-level window under the cursor. Used to tell whether a drop landed on a
    /// floating window: comparing bounds is not enough, since a floating window can sit behind
    /// the main one.
    /// </summary>
    internal static IntPtr TopLevelWindowAt(PointInt32 screen)
    {
        var hwnd = WindowFromPoint(new NativePoint { X = screen.X, Y = screen.Y });
        return hwnd == IntPtr.Zero ? IntPtr.Zero : GetAncestor(hwnd, GetAncestorRoot);
    }

    /// <summary>
    /// Makes <paramref name="owned"/> an owned window of <paramref name="owner"/>: it then
    /// stays above the owner, is not listed in the taskbar, and minimizes and closes with it,
    /// which is how tool windows floating out of a main window are expected to behave.
    /// </summary>
    internal static void SetOwner(IntPtr owned, IntPtr owner)
    {
        if (owned != IntPtr.Zero && owner != IntPtr.Zero)
        {
            SetWindowLongPtr(owned, GwlpHwndParent, owner);
        }
    }

    /// <summary>Gets the window handle backing the element's XAML island, if there is one.</summary>
    internal static IntPtr? GetWindowHandle(FrameworkElement element)
    {
        if (element.XamlRoot?.ContentIslandEnvironment is not { } environment)
        {
            return null;
        }

        var hwnd = Win32Interop.GetWindowFromWindowId(environment.AppWindowId);
        return hwnd == IntPtr.Zero ? null : hwnd;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint point);

    private const uint GetAncestorRoot = 2;
    private const int GwlpHwndParent = -8;

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
}
