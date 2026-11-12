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

    /// <summary>
    /// Opens a document, optionally as the group's provisional (preview) document: the tab Visual
    /// Studio shows at the end of the strip in italics, one at a time.
    /// </summary>
    /// <param name="document">The document to open.</param>
    /// <param name="provisional">
    /// <see langword="true"/> to open it in preview, which closes the document the group was
    /// previewing; <see langword="false"/> to open an ordinary tab, which also promotes the
    /// document when it was the one being previewed.
    /// </param>
    /// <remarks>
    /// Which documents open in preview is the application's decision — in Visual Studio a single
    /// click in Solution Explorer, Go To Definition, a search result or the debugger. The document
    /// being replaced is closed through <see cref="DockingWindow.Close"/>, so an application that
    /// will not let it go keeps it: it is promoted instead of closed, and the group is left with
    /// one provisional tab either way. See <see cref="DocumentWindow.IsProvisional"/>.
    /// </remarks>
    public void OpenDocument(DocumentWindow document, bool provisional)
    {
        ArgumentNullException.ThrowIfNull(document);
        LayoutManager.OpenDocument(this, document, provisional);
    }

    /// <summary>
    /// Arranges the documents of this area into the tab groups and splits a <see cref="DocumentLayout"/> describes, in one
    /// step: side by side, stacked, in a grid, or all in tabs of one group.
    /// </summary>
    /// <param name="layout">The arrangement. Each document is named once.</param>
    /// <remarks>
    /// <para>
    /// The documents are the same instances afterwards, moved: nothing they hold is recreated, and nothing is closed. A
    /// document the layout names that is closed is opened where the layout puts it; one in a floating window is brought
    /// back. A document open in this area that the layout does <em>not</em> name becomes a tab of the layout's last
    /// group, so no document is left out of the area or closed by the arrangement. A group the layout leaves with no
    /// document is dropped, and a split left with one pane is replaced by it, so the layout of "three columns" for two
    /// documents is two columns.
    /// </para>
    /// <para>
    /// Tool windows, auto-hidden panels and floating windows are not touched. The active document stays active, and
    /// <see cref="DockSite.LayoutChanged"/> is raised once, with <see cref="LayoutChangeKind.DocumentsArranged"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">A document is named twice.</exception>
    /// <exception cref="InvalidOperationException">This area is not part of a dock site.</exception>
    public void Arrange(DocumentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (this.FindSurface()?.Site is not { } site)
        {
            throw new InvalidOperationException("The document area is not part of a dock site, so there is nothing to arrange it in.");
        }

        var requested = ToArrangement(layout);
        var plan = DocumentArrangementPlanner.Plan(requested, Documents.ToList());
        if (plan is null)
        {
            return;
        }

        var documents = ArrangementItems(plan).ToList();
        LayoutManager.ArrangeDocuments(site, this, plan, documents);
    }

    private static ArrangementNode<DocumentWindow> ToArrangement(DocumentLayout layout)
    {
        return layout switch
        {
            DocumentGroupLayout group => new ArrangementGroup<DocumentWindow>(layout.RelativeSize, group.Documents, group.Selected),
            DocumentSplitLayout split => new ArrangementSplit<DocumentWindow>(
                layout.RelativeSize, split.Orientation, [.. split.Panes.Select(ToArrangement)]),
            _ => throw new ArgumentException("Unknown kind of document layout.", nameof(layout)),
        };
    }

    private static IEnumerable<DocumentWindow> ArrangementItems(ArrangementNode<DocumentWindow> node)
    {
        return node switch
        {
            ArrangementGroup<DocumentWindow> group => group.Items,
            ArrangementSplit<DocumentWindow> split => split.Children.SelectMany(ArrangementItems),
            _ => [],
        };
    }

    /// <summary>
    /// Closes the documents of this area in one go, the way "Close All Tabs" does in Visual Studio.
    /// </summary>
    /// <param name="scope">Which documents to close.</param>
    /// <remarks>
    /// Pinned documents survive every scope but <see cref="DocumentCloseScope.All"/>: that is what
    /// pinning a tab is for. Each document is closed on its own, so a document the application will
    /// not let go of — one with <see cref="DockingWindow.CanClose"/> cleared, or one a handler of
    /// <see cref="DockSite.WindowClosing"/> cancels — stays open while the rest close.
    /// </remarks>
    public void CloseDocuments(DocumentCloseScope scope)
    {
        var active = this.FindSurface()?.Site.ActiveDocument;

        // Over a snapshot: closing a document takes it out of the group being enumerated.
        foreach (var document in Documents.ToList())
        {
            if (scope != DocumentCloseScope.All && document.IsPinned)
            {
                continue;
            }

            if (scope == DocumentCloseScope.AllButActive && ReferenceEquals(document, active))
            {
                continue;
            }

            document.Close();
        }
    }

    UIElement? ILayoutHost.LayoutChild
    {
        get => Child;
        set => Child = value;
    }

    void IRelocatable.RaiseRelocated() => Relocated?.Invoke(this, EventArgs.Empty);
}
