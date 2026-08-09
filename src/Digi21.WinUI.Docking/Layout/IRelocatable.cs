namespace Digi21.WinUI.Docking;

/// <summary>
/// An element of the layout whose content belongs to the application, and which therefore has to
/// be told when a docking operation has moved it to a different place in the XAML tree.
/// </summary>
/// <remarks>
/// WinUI is of no help here. Moving an element within a single layout pass raises its
/// <see cref="Microsoft.UI.Xaml.FrameworkElement.Loaded"/> and
/// <see cref="Microsoft.UI.Xaml.FrameworkElement.Unloaded"/> events in that order — the unload
/// notification arrives last, for an element that is still in the tree and visible — so content
/// with a life cycle of its own (a swap chain, a media player, a render loop) that hangs off those
/// events is torn down and never brought back. This is the notification to rely on instead.
/// </remarks>
internal interface IRelocatable
{
    /// <summary>Raises the element's <c>Relocated</c> event.</summary>
    void RaiseRelocated();
}
