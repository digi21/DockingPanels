using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;

namespace Digi21.WinUI.Docking;

// Bridges XAML coordinates and screen coordinates. Floating windows are real top-level windows, so
// dragging one over the dock site spans two XAML islands with unrelated coordinate spaces:
// TransformToVisual cannot cross them and the pointer positions reported by each island are
// relative to its own window. Screen pixels are the only common reference, and the cursor position
// read from the system is the only reliable source while the dragged window is being moved under
// the pointer.
internal static class ScreenInterop
{
    // Gets the current cursor position in screen pixels.
    internal static PointInt32 CursorPosition
    {
        get => GetCursorPos(out var point) ? new PointInt32(point.X, point.Y) : default;
    }

    // Converts a point expressed in an element's own coordinates (device-independent pixels) into
    // screen pixels, or returns null when the element is not connected to a window yet.
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

    // Converts a screen point into an element's own coordinates (device-independent pixels), or
    // returns null when the element is not connected to a window yet.
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

    // Converts a size in device-independent pixels to screen pixels for the given element's window.
    internal static SizeInt32 ToScreenSize(FrameworkElement element, double width, double height)
    {
        var scale = element.XamlRoot?.RasterizationScale ?? 1.0;
        return new SizeInt32((int)Math.Round(width * scale), (int)Math.Round(height * scale));
    }

    // Gets the top-level window under the cursor. Used to tell whether a drop landed on a floating
    // window: comparing bounds is not enough, since a floating window can sit behind the main one.
    internal static IntPtr TopLevelWindowAt(PointInt32 screen)
    {
        var hwnd = WindowFromPoint(new NativePoint { X = screen.X, Y = screen.Y });
        return hwnd == IntPtr.Zero ? IntPtr.Zero : GetAncestor(hwnd, GetAncestorRoot);
    }

    // Makes owned an owned window of owner: it then stays above the owner, is not listed in the
    // taskbar, and minimizes and closes with it, which is how tool windows floating out of a main
    // window are expected to behave.
    internal static void SetOwner(IntPtr owned, IntPtr owner)
    {
        if (owned != IntPtr.Zero && owner != IntPtr.Zero)
        {
            SetWindowLongPtr(owned, GwlpHwndParent, owner);
        }
    }

    // Hands the foreground over to the window hosting the given element, and does nothing unless
    // the given window is the one currently holding it.
    //
    // Closing the foreground window leaves Windows to choose its successor, and it does not
    // reliably choose the owner: docking a floating window back sends the whole application behind
    // whatever else was on screen, as if the user had clicked another program. Handing the
    // foreground over explicitly, while the window that is about to close still holds it, is what
    // keeps the application in front (and what makes the call allowed in the first place: only the
    // process owning the foreground may set it).
    internal static void HandOverForeground(IntPtr closing, FrameworkElement element)
    {
        if (closing == IntPtr.Zero || GetForegroundWindow() != closing)
        {
            return;
        }

        if (GetWindowHandle(element) is { } successor)
        {
            SetForegroundWindow(successor);
        }
    }

    // Brings the top-level window hosting the given element to the foreground. Windows only honors
    // the call from the process that already owns the foreground, which is exactly the case this
    // exists for: activating a docked window while a floating window of the application has the
    // focus should surface the main window, the same way activating a floating window surfaces it.
    internal static void BringToForeground(FrameworkElement element)
    {
        if (GetWindowHandle(element) is { } hwnd && GetForegroundWindow() != hwnd)
        {
            SetForegroundWindow(hwnd);
        }
    }

    // Gets the window handle backing the element's XAML island, if there is one.
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
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
}
