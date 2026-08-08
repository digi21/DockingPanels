namespace Digi21.WinUI.Docking;

/// <summary>
/// Event data for <see cref="DockSite"/> events that concern a single <see cref="DockingWindow"/>.
/// </summary>
public class DockingWindowEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="DockingWindowEventArgs"/> class.</summary>
    /// <param name="window">The window the event refers to.</param>
    public DockingWindowEventArgs(DockingWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        Window = window;
    }

    /// <summary>Gets the window the event refers to.</summary>
    public DockingWindow Window { get; }
}

/// <summary>
/// Event data for the cancelable <see cref="DockSite.WindowClosing"/> event.
/// </summary>
public class DockingWindowClosingEventArgs : DockingWindowEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="DockingWindowClosingEventArgs"/> class.</summary>
    /// <param name="window">The window that is about to close.</param>
    public DockingWindowClosingEventArgs(DockingWindow window)
        : base(window)
    {
    }

    /// <summary>Gets or sets a value indicating whether the close operation should be canceled.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// Describes what kind of change the <see cref="DockSite.LayoutChanged"/> event reports.
/// </summary>
public enum LayoutChangeKind
{
    /// <summary>A window was opened (added to the layout).</summary>
    WindowOpened,

    /// <summary>A window was closed (removed from the layout).</summary>
    WindowClosed,

    /// <summary>A window was docked or re-docked to a new position.</summary>
    WindowDocked,

    /// <summary>A window group was collapsed to an auto-hide edge.</summary>
    WindowAutoHidden,

    /// <summary>A full layout was loaded, e.g. by the layout serializer.</summary>
    LayoutLoaded,
}

/// <summary>
/// Event data for the <see cref="DockSite.LayoutChanged"/> event.
/// </summary>
public class LayoutChangedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="LayoutChangedEventArgs"/> class.</summary>
    /// <param name="kind">The kind of layout change.</param>
    public LayoutChangedEventArgs(LayoutChangeKind kind)
    {
        Kind = kind;
    }

    /// <summary>Gets the kind of layout change that occurred.</summary>
    public LayoutChangeKind Kind { get; }
}
