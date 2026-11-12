using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking;

// How the library casts an object that native code handed it to a Windows Runtime class, such as
// a template part to TextBlock, in a way that keeps working under NativeAOT.
//
// A projected class such as TextBlock is never created by managed code reaching it: when native
// code hands back an object (a template part, a visual tree child, an AppWindow presenter) the
// projection reads its runtime class name and builds the wrapper through a factory that lives on
// the projected class. Nothing references that factory statically, and a cast only tests a type,
// so the AOT compiler concludes the class is never constructed and drops it. The wrapper then
// falls back to the nearest surviving base class (a FrameworkElement for a ContentPresenter) and
// the cast evaluates to null without any error: a container whose PART_ContentHost cannot be cast
// never plugs its windows into its template.
//
// The cast therefore has to say which class it needs. TemplatePart<T> and As<T> do that through
// the trimming annotation on T, so naming a class in a call is what keeps it, and there is no
// separate attribute to forget. Every lookup of a template part goes through TemplatePart<T>:
// BannedSymbols.txt makes a direct GetTemplateChild call a build error. A cast of anything else
// that native code returned to a WinUI class the library does not construct itself (the content of
// a button, a window presenter) goes through As<T>. Casts to FrameworkElement, UIElement, Control
// and the library's own types of an object that is already managed need nothing.
//
// The annotation matches what CsWinRT's [DynamicWindowsRuntimeCast(typeof(T))] makes its source
// generator emit. That attribute and the analyzer for it only ship with a WinRT.Runtime that a
// net8.0 target cannot reference, so the annotation is applied directly.
internal static class ProjectedClassCast
{
    // The members of a projected class to keep: the same ones CsWinRT's own generator asks for
    // (its non-public constructors, which include the one wrapping a native object, and its
    // public fields).
    internal const DynamicallyAccessedMemberTypes Members =
        DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicFields;

    // Gets the template part with the given name as a T, or null when the template has no such
    // part or it is not a T. T has to be the concrete class the template declares.
    internal static T? TemplatePart<[DynamicallyAccessedMembers(Members)] T>(this Control owner, string name)
        where T : class
        => GetTemplateChild(owner, name) as T;

    // Gets a tab strip or other panel part. Panel is abstract, so keeping it keeps nothing a
    // wrapper could be: the class to keep is the one the default templates declare, StackPanel.
    internal static Panel? TemplatePanel(this Control owner, string name)
    {
        Keep<StackPanel>();
        return owner.TemplatePart<Panel>(name);
    }

    // Casts an object native code returned to T, or gives null.
    internal static T? As<[DynamicallyAccessedMembers(Members)] T>(object? value)
        where T : class
        => value as T;

    // Reaches Control.GetTemplateChild, which is protected, from here. This is the one place that
    // lookup happens, which is also why it is not a call the ban in BannedSymbols.txt can see.
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetTemplateChild")]
    private static extern DependencyObject? GetTemplateChild(Control owner, string childName);

    // Keeps T. Calling it is what the annotation on T acts on.
    private static void Keep<[DynamicallyAccessedMembers(Members)] T>()
    {
    }
}
