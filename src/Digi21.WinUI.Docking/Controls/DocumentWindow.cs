using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

/// <summary>
/// A document of the tabbed MDI area. Documents are hosted in a <see cref="DocumentContainer"/>
/// inside a <see cref="DocumentHost"/>, become tabs when several share a group, and can be
/// dragged into new tab groups or floated out into their own window.
/// </summary>
/// <remarks>
/// Documents never auto-hide and never dock to the sides of the dock site: the document area is
/// theirs, and tool windows dock around it.
/// </remarks>
public partial class DocumentWindow : DockingWindow
{
    /// <summary>Identifies the <see cref="IsPinned"/> dependency property.</summary>
    public static readonly DependencyProperty IsPinnedProperty = DependencyProperty.Register(
        nameof(IsPinned),
        typeof(bool),
        typeof(DocumentWindow),
        new PropertyMetadata(false, (d, _) => ((DocumentWindow)d).OnIsPinnedChanged()));

    /// <summary>Initializes a new instance of the <see cref="DocumentWindow"/> class.</summary>
    public DocumentWindow()
    {
        DefaultStyleKey = typeof(DocumentWindow);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");

        // The templates below reference the library's own brush and metric keys; this is what
        // puts them within reach of the application's resources.
        DockingThemeResources.Ensure();

        CanAutoHide = false;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the document's tab is pinned to the left end of its
    /// group, as pinning a tab does in Visual Studio: pinned tabs keep their own block and their
    /// own order at the head of the strip, stay in view when the strip overflows, and are spared by
    /// <see cref="DocumentHost.CloseDocuments"/> unless it is asked to close everything.
    /// </summary>
    /// <remarks>
    /// This has nothing to do with the pin button of a <see cref="ToolWindow"/>'s title bar, which
    /// toggles auto-hiding: see <see cref="DockingWindow.CanAutoHide"/> and
    /// <see cref="DockingWindow.AutoHide"/>. A document is never auto-hidden and a tool window is
    /// never pinned in this sense.
    /// </remarks>
    public bool IsPinned
    {
        get => (bool)GetValue(IsPinnedProperty);
        set => SetValue(IsPinnedProperty, value);
    }

    /// <summary>
    /// Pins the document's tab to the left end of its group, moving it to the end of the pinned
    /// block. Does nothing when it is already pinned.
    /// </summary>
    public void Pin() => IsPinned = true;

    /// <summary>
    /// Unpins the document's tab, moving it to the front of the normal block. Does nothing when it
    /// is not pinned.
    /// </summary>
    public void Unpin() => IsPinned = false;

    private void OnIsPinnedChanged() => LayoutManager.MoveToOwnBlock(this);
}
