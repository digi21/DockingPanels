namespace Digi21.WinUI.Docking.Serialization;

/// <summary>
/// Event data for <see cref="DockSiteLayoutSerializer.ToolWindowResolving"/>, raised while
/// loading a layout when a serialized window id does not match any registered tool window.
/// Handlers can create the window on demand and assign it to <see cref="ToolWindow"/>.
/// </summary>
public class ToolWindowResolvingEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ToolWindowResolvingEventArgs"/> class.</summary>
    /// <param name="id">The serialization id that could not be resolved.</param>
    public ToolWindowResolvingEventArgs(string id)
    {
        Id = id;
    }

    /// <summary>Gets the serialization id that could not be resolved.</summary>
    public string Id { get; }

    /// <summary>Gets or sets the lazily created window to use, or <see langword="null"/> to skip the entry.</summary>
    public ToolWindow? ToolWindow { get; set; }
}

/// <summary>
/// What to do with windows that are open when a layout is loaded but do not appear in it.
/// </summary>
public enum UnresolvedWindowBehavior
{
    /// <summary>Close the window. It stays registered and can be reopened later.</summary>
    Close,

    /// <summary>
    /// Keep the window open: tool windows dock to the left edge of the dock site, documents
    /// reopen in the document area.
    /// </summary>
    DockLeft,
}
