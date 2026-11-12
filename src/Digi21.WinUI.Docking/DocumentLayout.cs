using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking;

/// <summary>
/// A description of how the documents of a <see cref="DocumentHost"/> are arranged into tab groups and splits, to be
/// applied with <see cref="DocumentHost.Arrange"/>. Build one with <see cref="Group(DocumentWindow[])"/> and
/// <see cref="Split(Orientation, DocumentLayout[])"/>:
/// </summary>
/// <example>
/// A grid of four, two on top and two below, each document in a group of its own:
/// <code>
/// host.Arrange(DocumentLayout.Split(Orientation.Vertical,
///     DocumentLayout.Split(Orientation.Horizontal, DocumentLayout.Group(a), DocumentLayout.Group(b)),
///     DocumentLayout.Split(Orientation.Horizontal, DocumentLayout.Group(c), DocumentLayout.Group(d))));
/// </code>
/// One large group on the left taking 60 percent, two stacked on the right:
/// <code>
/// host.Arrange(DocumentLayout.Split(Orientation.Horizontal,
///     DocumentLayout.Group(a).WithRelativeSize(0.6),
///     DocumentLayout.Split(Orientation.Vertical, DocumentLayout.Group(b), DocumentLayout.Group(c)).WithRelativeSize(0.4)));
/// </code>
/// </example>
public abstract class DocumentLayout
{
    internal DocumentLayout()
    {
    }

    /// <summary>
    /// Gets the share of space this node takes among its siblings in the split that holds it. Only the proportions
    /// between siblings matter (2 and 1 is the same as 0.667 and 0.333); the default is 1, so siblings share equally.
    /// </summary>
    public double RelativeSize { get; private set; } = 1.0;

    /// <summary>Sets <see cref="RelativeSize"/> and returns this node, so that it can be written inside a layout expression.</summary>
    /// <param name="relativeSize">A positive number.</param>
    /// <returns>This node.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="relativeSize"/> is not a positive, finite number.</exception>
    public DocumentLayout WithRelativeSize(double relativeSize)
    {
        if (!double.IsFinite(relativeSize) || relativeSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(relativeSize), relativeSize, "A relative size must be a positive, finite number.");
        }

        RelativeSize = relativeSize;
        return this;
    }

    /// <summary>A group of tabs holding the given documents, in order; the first is shown.</summary>
    /// <param name="documents">The documents of the group.</param>
    /// <returns>The group.</returns>
    public static DocumentGroupLayout Group(params DocumentWindow[] documents) => new(documents, selected: null);

    /// <summary>A group of tabs holding the given documents, in order.</summary>
    /// <param name="documents">The documents of the group.</param>
    /// <param name="selected">The document shown, or <see langword="null"/> for the first one.</param>
    /// <returns>The group.</returns>
    /// <exception cref="ArgumentException"><paramref name="selected"/> is not one of <paramref name="documents"/>.</exception>
    public static DocumentGroupLayout Group(IEnumerable<DocumentWindow> documents, DocumentWindow? selected = null) =>
        new([.. documents], selected);

    /// <summary>Panes side by side (<see cref="Orientation.Horizontal"/>) or stacked (<see cref="Orientation.Vertical"/>).</summary>
    /// <param name="orientation">The direction the panes are laid out in.</param>
    /// <param name="panes">The groups and splits it holds; each takes its <see cref="RelativeSize"/> of the space.</param>
    /// <returns>The split.</returns>
    public static DocumentSplitLayout Split(Orientation orientation, params DocumentLayout[] panes) => new(orientation, panes);
}

/// <summary>A tab group in a <see cref="DocumentLayout"/>: the documents it holds, in tab order.</summary>
public sealed class DocumentGroupLayout : DocumentLayout
{
    internal DocumentGroupLayout(DocumentWindow[] documents, DocumentWindow? selected)
    {
        ArgumentNullException.ThrowIfNull(documents);
        foreach (var document in documents)
        {
            ArgumentNullException.ThrowIfNull(document, nameof(documents));
        }

        if (selected is not null && Array.IndexOf(documents, selected) < 0)
        {
            throw new ArgumentException("The selected document is not one of the group's documents.", nameof(selected));
        }

        Documents = documents;
        Selected = selected;
    }

    /// <summary>Gets the documents of the group, in tab order.</summary>
    public IReadOnlyList<DocumentWindow> Documents { get; }

    /// <summary>Gets the document shown, or <see langword="null"/> when the first one is.</summary>
    public DocumentWindow? Selected { get; }
}

/// <summary>A split in a <see cref="DocumentLayout"/>: panes side by side or stacked.</summary>
public sealed class DocumentSplitLayout : DocumentLayout
{
    internal DocumentSplitLayout(Orientation orientation, DocumentLayout[] panes)
    {
        ArgumentNullException.ThrowIfNull(panes);
        foreach (var pane in panes)
        {
            ArgumentNullException.ThrowIfNull(pane, nameof(panes));
        }

        Orientation = orientation;
        Panes = panes;
    }

    /// <summary>Gets the direction the panes are laid out in.</summary>
    public Orientation Orientation { get; }

    /// <summary>Gets the groups and splits this one holds, in layout order.</summary>
    public IReadOnlyList<DocumentLayout> Panes { get; }
}
