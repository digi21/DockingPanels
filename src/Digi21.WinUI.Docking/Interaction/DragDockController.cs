using System.Runtime.InteropServices;
using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Windows.Graphics;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Runs the interactive re-docking gesture for a <see cref="DockSite"/>: pointer-capture based
/// dragging of tool windows, dock guide display, drop target hit-testing, and the final layout
/// mutation on drop. The XAML drag-and-drop API is deliberately not used: it is a data transfer
/// API with system-drawn visuals and little mid-drag control.
/// </summary>
/// <remarks>
/// A drag can cross windows, so the gesture is tracked in screen coordinates and the guides are
/// shown on the <see cref="IDockSurface"/> under the cursor: the dock site, or any of its floating
/// windows. That is what lets a window be dropped inside a floating window instead of only onto
/// it. Dragging the caption of a floating window is a second mode: the gesture starts in another
/// top-level window and the window itself follows the cursor.
/// </remarks>
internal sealed partial class DragDockController
{
    private const double DragThreshold = 4.0;
    private const int ExternalPollMilliseconds = 16;

    private readonly DockSite site;

    private ToolWindow? draggedWindow;
    private UIElement? source;
    private Pointer? pointer;
    private Point startPoint;
    private bool dragActive;
    private bool showGhost;
    private IDockSurface? activeSurface;
    private DockTarget currentTarget;

    private FloatingWindowHost? draggedHost;
    private bool movesHost;
    private DispatcherQueueTimer? externalTimer;
    private PointInt32 startCursor;
    private PointInt32 startHostPosition;
    private PointInt32 lastCursor;

    internal DragDockController(DockSite site)
    {
        this.site = site;
    }

    /// <summary>
    /// Starts tracking a pointer press on a tab or title bar. The drag only becomes visible
    /// once the pointer moves past a small threshold, so plain clicks are unaffected.
    /// </summary>
    internal void BeginPotentialDrag(ToolWindow window, UIElement sourceElement, PointerRoutedEventArgs e)
    {
        if (draggedWindow is not null || !window.CanDragWindow || !window.IsOpen)
        {
            return;
        }

        if (window.State == DockingWindowState.Floating)
        {
            if (site.FindFloatingHost(window) is { } host)
            {
                // In a floating window with a single pane the title bar is the window's caption,
                // so dragging it moves the whole window; with several panes it belongs to one of
                // them, and dragging it takes only that window, as it does when docked.
                var movesTheHost = sourceElement is ToolWindowTitleBar && host.SinglePane is not null;
                BeginExternalDrag(window, sourceElement, e, host, movesTheHost);
            }

            return;
        }

        if (!sourceElement.CapturePointer(e.Pointer))
        {
            return;
        }

        draggedWindow = window;
        source = sourceElement;
        pointer = e.Pointer;
        startPoint = e.GetCurrentPoint(site).Position;
        dragActive = false;
        currentTarget = DockTarget.None;

        sourceElement.PointerMoved += OnPointerMoved;
        sourceElement.PointerReleased += OnPointerReleased;
        sourceElement.PointerCaptureLost += OnPointerCaptureLost;
    }

    /// <summary>Starts dragging a whole floating window by its caption.</summary>
    internal void BeginHostDrag(FloatingWindowHost host, UIElement sourceElement, PointerRoutedEventArgs e)
    {
        if (draggedWindow is not null || host.Windows.FirstOrDefault() is not { } window)
        {
            return;
        }

        BeginExternalDrag(window, sourceElement, e, host, movesTheHost: true);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (draggedWindow is null)
        {
            return;
        }

        if (!dragActive)
        {
            var point = e.GetCurrentPoint(site).Position;
            if (Math.Abs(point.X - startPoint.X) < DragThreshold && Math.Abs(point.Y - startPoint.Y) < DragThreshold)
            {
                return;
            }

            StartDrag(withGhost: true);
        }

        UpdateDrag();
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var window = draggedWindow;
        var surface = activeSurface;
        var target = currentTarget;
        var cursor = ScreenInterop.CursorPosition;
        var wasActive = dragActive;

        EndDrag();

        if (wasActive && window is not null)
        {
            Drop(surface, target, window, cursor);
        }

        e.Handled = wasActive;
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        EndDrag();
    }

    /// <summary>Starts showing the drag feedback, on whichever surface the cursor is over.</summary>
    private void StartDrag(bool withGhost)
    {
        dragActive = true;
        showGhost = withGhost;
        activeSurface = null;
    }

    /// <summary>
    /// Follows the cursor: moves the guides to the surface under it and resolves what dropping
    /// there would do.
    /// </summary>
    private void UpdateDrag()
    {
        var cursor = ScreenInterop.CursorPosition;
        var surface = SurfaceAt(cursor);
        var point = surface is null ? null : ScreenInterop.FromScreen(surface.Root, cursor);

        if (surface?.Overlay is not { } overlay || point is not { } local || !overlay.Contains(local))
        {
            LeaveSurface();
            return;
        }

        if (!ReferenceEquals(surface, activeSurface))
        {
            LeaveSurface();
            activeSurface = surface;
            overlay.Begin(showGhost, draggedWindow?.Title ?? string.Empty);
        }

        currentTarget = overlay.Update(local);
    }

    private void LeaveSurface()
    {
        activeSurface?.Overlay?.Reset();
        activeSurface = null;
        currentTarget = DockTarget.None;
    }

    /// <summary>Finds the docking surface the cursor is over, if any.</summary>
    private IDockSurface? SurfaceAt(PointInt32 cursor)
    {
        if (movesHost)
        {
            // The dragged window follows the cursor and covers whatever is under it, so what the
            // topmost window is says nothing here: the surface has to be found by geometry.
            for (var i = site.FloatingHosts.Count - 1; i >= 0; i--)
            {
                var candidate = site.FloatingHosts[i];
                if (!ReferenceEquals(candidate, draggedHost) && Contains(candidate.Bounds, cursor))
                {
                    return candidate.Surface;
                }
            }

            return site;
        }

        var handle = ScreenInterop.TopLevelWindowAt(cursor);
        if (site.FindFloatingHost(handle) is { } host)
        {
            return host.Surface;
        }

        return ScreenInterop.GetWindowHandle(site) == handle ? site : null;
    }

    private static bool Contains(RectInt32 bounds, PointInt32 point)
    {
        return point.X >= bounds.X
            && point.Y >= bounds.Y
            && point.X < bounds.X + bounds.Width
            && point.Y < bounds.Y + bounds.Height;
    }

    private void EndDrag()
    {
        if (source is not null)
        {
            source.PointerMoved -= OnPointerMoved;
            source.PointerReleased -= OnPointerReleased;
            source.PointerCaptureLost -= OnPointerCaptureLost;

            if (pointer is not null)
            {
                source.ReleasePointerCapture(pointer);
            }
        }

        LeaveSurface();

        draggedWindow = null;
        source = null;
        pointer = null;
        dragActive = false;
        showGhost = false;
    }

    /// <summary>Applies the drop of a single dragged window.</summary>
    private void Drop(IDockSurface? surface, DockTarget target, ToolWindow window, PointInt32 cursor)
    {
        if (surface is not null && target.Kind != DockTargetKind.None)
        {
            LayoutManager.DockAtTarget(surface, window, target);
            return;
        }

        // Released away from every guide: the window leaves the layout and floats, which
        // is how a floating window is created with the mouse.
        FloatAtCursor(window, cursor);
    }

    /// <summary>Floats a dragged window under the cursor, keeping the size it had while docked.</summary>
    private void FloatAtCursor(ToolWindow window, PointInt32 cursor)
    {
        if (!window.CanFloat)
        {
            return;
        }

        var (width, height) = FloatingWindowHost.PreferredSize(window.Container);
        var size = ScreenInterop.ToScreenSize(site, width, height);

        // Put the caption under the cursor, roughly where the user was holding the window.
        var scale = site.XamlRoot?.RasterizationScale ?? 1.0;
        var origin = new PointInt32(cursor.X - (int)Math.Round(24 * scale), cursor.Y - (int)Math.Round(12 * scale));

        site.FloatToolWindow(window, new RectInt32(origin.X, origin.Y, size.Width, size.Height));
    }

    /// <summary>
    /// Starts dragging a floating window (by its caption or by the title bar that acts as one),
    /// or one of the windows inside it. The gesture lives in another top-level window, so it is
    /// tracked in screen coordinates: while the window follows the cursor its pointer never
    /// changes position inside the window, and XAML would stop reporting moves.
    /// </summary>
    private void BeginExternalDrag(
        ToolWindow window,
        UIElement sourceElement,
        PointerRoutedEventArgs e,
        FloatingWindowHost host,
        bool movesTheHost)
    {
        sourceElement.CapturePointer(e.Pointer);

        draggedWindow = window;
        draggedHost = host;
        movesHost = movesTheHost;
        source = sourceElement;
        pointer = e.Pointer;
        dragActive = false;
        currentTarget = DockTarget.None;
        startCursor = ScreenInterop.CursorPosition;
        lastCursor = startCursor;

        var bounds = host.Bounds;
        startHostPosition = new PointInt32(bounds.X, bounds.Y);

        sourceElement.PointerReleased += OnExternalPointerReleased;
        sourceElement.PointerCaptureLost += OnExternalPointerReleased;

        externalTimer = site.DispatcherQueue.CreateTimer();
        externalTimer.Interval = TimeSpan.FromMilliseconds(ExternalPollMilliseconds);
        externalTimer.IsRepeating = true;
        externalTimer.Tick += OnExternalTick;
        externalTimer.Start();
    }

    private void OnExternalTick(DispatcherQueueTimer sender, object args)
    {
        if (draggedHost is null)
        {
            EndExternalDrag(drop: false);
            return;
        }

        if (!IsPrimaryButtonDown())
        {
            // The button came up over another window, so no pointer event will arrive here.
            EndExternalDrag(drop: true);
            return;
        }

        var cursor = ScreenInterop.CursorPosition;
        if (cursor.X == lastCursor.X && cursor.Y == lastCursor.Y)
        {
            return;
        }

        lastCursor = cursor;

        if (!dragActive)
        {
            var threshold = DragThreshold * (site.XamlRoot?.RasterizationScale ?? 1.0);
            if (Math.Abs(cursor.X - startCursor.X) < threshold && Math.Abs(cursor.Y - startCursor.Y) < threshold)
            {
                return;
            }

            // The moved window is its own drag visual; a window pulled out of it needs the ghost.
            StartDrag(withGhost: !movesHost);
        }

        if (movesHost)
        {
            draggedHost.MoveTo(new PointInt32(
                startHostPosition.X + cursor.X - startCursor.X,
                startHostPosition.Y + cursor.Y - startCursor.Y));
        }

        UpdateDrag();
    }

    private void OnExternalPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        EndExternalDrag(drop: true);
    }

    private void EndExternalDrag(bool drop)
    {
        var window = draggedWindow;
        var host = draggedHost;
        var surface = activeSurface;
        var target = currentTarget;
        var cursor = ScreenInterop.CursorPosition;
        var wasActive = dragActive;
        var wasMovingHost = movesHost;

        if (externalTimer is not null)
        {
            externalTimer.Stop();
            externalTimer.Tick -= OnExternalTick;
            externalTimer = null;
        }

        if (source is not null)
        {
            source.PointerReleased -= OnExternalPointerReleased;
            source.PointerCaptureLost -= OnExternalPointerReleased;

            if (pointer is not null)
            {
                source.ReleasePointerCapture(pointer);
            }
        }

        LeaveSurface();

        draggedWindow = null;
        draggedHost = null;
        source = null;
        pointer = null;
        dragActive = false;
        showGhost = false;
        movesHost = false;

        if (!drop || !wasActive || window is null || host is null)
        {
            return;
        }

        var hasTarget = surface is not null && target.Kind != DockTargetKind.None;

        // Docking closes the floating window this gesture started in; let its input event
        // finish first so the window is not torn down from inside its own handler.
        site.DispatcherQueue.TryEnqueue(() =>
        {
            if (wasMovingHost)
            {
                if (hasTarget)
                {
                    LayoutManager.DockFloatingHost(site, host, target, surface!);
                }
            }
            else if (hasTarget)
            {
                LayoutManager.DockAtTarget(surface!, window, target);
            }
            else if (host.Windows.Skip(1).Any())
            {
                // A window dropped outside every guide becomes a floating window of its own,
                // unless it was already alone in the one it was dragged out of.
                FloatAtCursor(window, cursor);
            }
        });
    }

    private static bool IsPrimaryButtonDown() => (GetAsyncKeyState(VirtualKeyLeftButton) & 0x8000) != 0;

    private const int VirtualKeyLeftButton = 0x01;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
