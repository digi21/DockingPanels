using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking;

/// <summary>
/// A tab group of the document area: hosts <see cref="DocumentWindow"/> instances as tabs shown
/// along the top of the group, like the editor tabs of Visual Studio.
/// </summary>
/// <remarks>
/// Several groups can live side by side inside a <see cref="DocumentHost"/>, split by
/// <see cref="SplitContainer"/> panes; dragging a document tab onto a side guide of a group
/// creates a new one. Unlike a <see cref="ToolWindowContainer"/>, a document group shows its
/// tabs even when it holds a single document, and has no title bar.
/// </remarks>
public partial class DocumentContainer : DockingWindowContainer
{
    /// <summary>Initializes a new instance of the <see cref="DocumentContainer"/> class.</summary>
    public DocumentContainer()
    {
        DefaultStyleKey = typeof(DocumentContainer);
        DefaultStyleResourceUri = new Uri("ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml");

        // The templates below reference the library's own brush and metric keys; this is what
        // puts them within reach of the application's resources.
        DockingThemeResources.Ensure();
    }

    /// <inheritdoc />
    protected override Control CreateTab(DockingWindow window) => new DocumentTabItem { Window = window };

    /// <summary>Document tabs are always shown: they are how a document is named and closed.</summary>
    /// <param name="count">The number of documents currently hosted.</param>
    protected override bool ShowTabs(int count) => count > 0;
}
