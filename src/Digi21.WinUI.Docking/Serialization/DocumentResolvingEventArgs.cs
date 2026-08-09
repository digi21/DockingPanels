namespace Digi21.WinUI.Docking.Serialization;

/// <summary>
/// Event data for <see cref="DockSiteLayoutSerializer.DocumentResolving"/>, raised while loading
/// a layout when a serialized document id does not match any registered document. Handlers can
/// reopen the document on demand and assign it to <see cref="Document"/>.
/// </summary>
public class DocumentResolvingEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="DocumentResolvingEventArgs"/> class.</summary>
    /// <param name="id">The serialization id that could not be resolved.</param>
    public DocumentResolvingEventArgs(string id)
    {
        Id = id;
    }

    /// <summary>Gets the serialization id that could not be resolved.</summary>
    public string Id { get; }

    /// <summary>Gets or sets the lazily created document to use, or <see langword="null"/> to skip the entry.</summary>
    public DocumentWindow? Document { get; set; }
}
