using Microsoft.UI.Xaml;

namespace Digi21.WinUI.Docking;

// Makes the library's brushes and metrics (Themes/DockingResources.xaml) reachable from the
// application's resources, which is what lets an application override any of them.
//
// A {ThemeResource} used inside a control template resolves against the dictionary the template was
// parsed in before it looks at Application.Resources. Keeping the keys in Themes/Generic.xaml next
// to the templates would therefore make them win over anything the application declares, and the
// chrome could only be recolored by retemplating it.
//
// The dictionary is merged at the bottom of the collection, so it acts as a set of defaults: keys
// the application declares directly, and dictionaries it merges itself, are looked up first. This
// mirrors what XamlControlsResources does for WinUI's own controls, except that the application
// does not have to add anything to its App.xaml.
internal static class DockingThemeResources
{
    private const string Source = "ms-appx:///Digi21.WinUI.Docking/Themes/DockingResources.xaml";

    private static bool merged;

    // Merges the dictionary the first time a docking control is created, which is always before any
    // of their templates is applied.
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

    // Reads one of the library's keys from the application's resources, falling back to the
    // built-in value when it is not defined (or when there is no application, as in a test).
    //
    // Only for the handful of values the layout and drag code impose from code rather than through
    // a template — a splitter's thickness, a guide's glyph. They are the same in every theme, so
    // they live in the root of the dictionary and not in its theme dictionaries.
    internal static T Value<T>(string key, T fallback)
    {
        Ensure();

        return Application.Current?.Resources is { } resources
            && resources.TryGetValue(key, out var value)
            && value is T typed
                ? typed
                : fallback;
    }
}
