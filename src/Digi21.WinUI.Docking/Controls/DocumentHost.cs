using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;

namespace Digi21.WinUI.Docking;

/// <summary>
/// The tabbed MDI area of a <see cref="DockSite"/>: it hosts the documents, split into as many
/// tab groups as the user creates, while tool windows dock around it.
/// </summary>
/// <remarks>
/// Its content is a layout tree of its own, made of <see cref="DocumentContainer"/> groups and
/// <see cref="SplitContainer"/> panes, so dropping a document on the side of a group splits the
/// area exactly as docking a tool window splits the dock site. An empty document host is a drop
/// target itself, so the last closed document can be brought back.
/// </remarks>
[ContentProperty(Name = nameof(Child))]
public partial class DocumentHost : Control, ILayoutHost, IRelocatable
{
    /// <summary>
    /// Raised after a docking operation has moved this document area to a different place in the
    /// XAML tree, once the tree has settled. Content with a life cycle of its own — a
    /// <c>SwapChainPanel</c>, a <c>WebView2</c>, a render loop — should hang off this event
    /// instead of off <see cref="FrameworkElement.Loaded"/> and
    /// <see cref="FrameworkElement.Unloaded"/>, which WinUI raises in that order for an element
    /// that never left the tree.
    /// </summary>
    public event EventHandler? Relocated;

    /// <summary>Identifies the <see cref="Child"/> dependency property.</summary>
    public static readonly DependencyProperty ChildProperty = DependencyProperty.Register(
        nameof(Child),
        typeof(UIElement),
        typeof(DocumentHost),
        new PropertyMetadata(null));

    /// <summary>Initializes a new instance of the <see cref="DocumentHost"/> class.</summary>
    public DocumentHost()
    {
        DefaultStyleKey = typeof(DocumentHost);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");

        // The templates below reference the library's own brush and metric keys; this is what
        // puts them within reach of the application's resources.
        DockingThemeResources.Ensure();
    }

    /// <summary>Gets or sets the root of the document area's layout tree.</summary>
    public UIElement? Child
    {
        get => (UIElement?)GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    /// <summary>Gets the documents open in this host, across all its tab groups.</summary>
    public IEnumerable<DocumentWindow> Documents => LayoutTree.Windows(Child).OfType<DocumentWindow>();

    /// <summary>Gets the tab groups of this host, in layout order.</summary>
    public IEnumerable<DocumentContainer> Groups => LayoutTree.Containers(Child).OfType<DocumentContainer>();

    /// <summary>
    /// Gets the group new documents open in: the one holding the active document, or the first
    /// group of the area when no document is active.
    /// </summary>
    public DocumentContainer? ActiveGroup
    {
        get
        {
            var groups = Groups.ToList();
            if (this.FindSurface()?.Site.ActiveWindow is { } active
                && groups.FirstOrDefault(g => g.Items.Contains(active)) is { } activeGroup)
            {
                return activeGroup;
            }

            return groups.FirstOrDefault();
        }
    }

    /// <summary>
    /// Opens a document as a new tab of the active group, creating the first group when the
    /// document area is empty. Also reopens closed documents.
    /// </summary>
    /// <param name="document">The document to open.</param>
    public void OpenDocument(DocumentWindow document)
    {
        ArgumentNullException.ThrowIfNull(document);
        LayoutManager.OpenDocument(this, document);
    }

    UIElement? ILayoutHost.LayoutChild
    {
        get => Child;
        set => Child = value;
    }

    void IRelocatable.RaiseRelocated() => Relocated?.Invoke(this, EventArgs.Empty);
}
