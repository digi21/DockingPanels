namespace Digi21.WinUI.Docking.Serialization;

/// <summary>
/// Event data for <see cref="DockSiteLayoutSerializer.UnresolvedWindowDocking"/>, raised while
/// loading a layout for each open window the layout does not mention and which is being kept open
/// instead of closed.
/// </summary>
/// <remarks>
/// Where the window lands is the application's business: the layout file has nothing to say about
/// a window it never heard of. <see cref="Side"/> is the edge it is about to dock at, and
/// <see cref="Handled"/> is how a handler that wants somewhere a single edge cannot express —
/// beside a particular pane, or as a tab of the group the window belongs with — takes over.
/// </remarks>
public class UnresolvedWindowEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="UnresolvedWindowEventArgs"/> class.</summary>
    /// <param name="window">The window being kept open.</param>
    /// <param name="side">The edge the window is about to dock at.</param>
    public UnresolvedWindowEventArgs(DockingWindow window, DockSide side)
    {
        Window = window;
        Side = side;
    }

    /// <summary>Gets the open window the loaded layout does not mention.</summary>
    public DockingWindow Window { get; }

    /// <summary>
    /// Gets or sets the dock site edge the window docks at as a new pane. Starts at the window's
    /// <see cref="ToolWindow.PreferredDockSide"/>, and is ignored for a document, which reopens in
    /// the document area.
    /// </summary>
    public DockSide Side { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the handler has placed the window itself, leaving
    /// the serializer nothing to do with it.
    /// </summary>
    /// <remarks>
    /// A handler that sets this is responsible for the window ending up somewhere: one left out of
    /// the layout stays open but unattached, which is the state it was in when the event was
    /// raised.
    /// </remarks>
    public bool Handled { get; set; }
}
