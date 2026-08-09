using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

/// <summary>
/// Makes the library's brushes and metrics (<c>Themes/DockingResources.xaml</c>) reachable from
/// the application's resources, which is what lets an application override any of them.
/// </summary>
/// <remarks>
/// <para>
/// A <c>{ThemeResource}</c> used inside a control template resolves against the dictionary the
/// template was parsed in before it looks at <see cref="Application.Resources"/>. Keeping the
/// keys in <c>Themes/Generic.xaml</c> next to the templates would therefore make them win over
/// anything the application declares, and the chrome could only be recolored by retemplating it.
/// </para>
/// <para>
/// The dictionary is merged at the <em>bottom</em> of the collection, so it acts as a set of
/// defaults: keys the application declares directly, and dictionaries it merges itself, are
/// looked up first. This mirrors what <c>XamlControlsResources</c> does for WinUI's own controls,
/// except that the application does not have to add anything to its <c>App.xaml</c>.
/// </para>
/// </remarks>
internal static class DockingThemeResources
{
    private const string Source = "ms-appx:///Digi21.WinUI.Docking/Themes/DockingResources.xaml";

    private static bool merged;

    /// <summary>
    /// Merges the dictionary the first time a docking control is created, which is always before
    /// any of their templates is applied.
    /// </summary>
    internal static void Ensure()
    {
        // Application.Current is null while the XAML designer or a unit test creates controls
        // without an application; there is nothing to merge into, and no template to break.
        if (merged || Application.Current is not { } application)
        {
            return;
        }

        merged = true;
        application.Resources.MergedDictionaries.Insert(0, new ResourceDictionary { Source = new Uri(Source) });
    }
}
