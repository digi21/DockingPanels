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
    /// <summary>Initializes a new instance of the <see cref="DocumentWindow"/> class.</summary>
    public DocumentWindow()
    {
        DefaultStyleKey = typeof(DocumentWindow);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");
        CanAutoHide = false;
    }
}
