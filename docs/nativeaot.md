# NativeAOT

`Digi21.WinUI.Docking` works in an app published with `PublishAot=true`. The library needs no trimmer
root descriptors and no `TrimmerRootAssembly`, and nothing in the app has to mention it.

It has been checked in an unpackaged, self-contained WinUI 3 app on .NET 10 with Windows App SDK
2.6.5: a `DockSite` with tool windows and documents, and floating a window and auto-hiding another.

## Project setup

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
</PropertyGroup>
```

`AllowUnsafeBlocks` is what the CsWinRT analyzer asks for (warning `CsWinRT1030`) in any project that
passes types implementing generic WinRT interfaces, such as a `DockingWindow` or a collection of
them, across the WinRT boundary. An app that declares docking windows in XAML does. CsWinRT generates
the NativeAOT marshalling code for those types with unsafe code, so follow the warning.

If your publish fails in the XAML compiler, that is unrelated to this library. The test app used
`UseOptimizedPublish=false` for Windows App SDK issue 6897.

## If a part of a docking control is missing or blank

Under NativeAOT a Windows Runtime class that your code only casts to, never constructs, can be
removed by the compiler. The object then comes back as a plain `FrameworkElement` and the cast
quietly returns `null`. The symptom in a docking layout is that window content is missing, tabs and
title bars have no text, or a button has no glyph, with no error.

The library keeps the classes the default templates use, so the default look is not affected. It
matters in two places:

- **A custom template.** The controls look up their template parts by name and expect a particular
  class. Use these classes, which the library keeps, or a class your application keeps (see below):

  | Part | Class |
  |---|---|
  | `PART_ContentHost`, `PART_LayoutRoot` | `ContentPresenter` |
  | `PART_Title`, `PART_DragGhostText` | `TextBlock` |
  | `PART_CloseButton`, `PART_PinButton`, `PART_PinTabButton` | `Button`, with a `FontIcon` as its content for the glyph |
  | `PART_TabStrip`, `PART_PinnedTabStrip`, `PART_ProvisionalTabStrip` | a `Panel`; the library keeps `StackPanel` |
  | `PART_Items` | `Grid` |
  | `PART_DragGhost` | `Border` |
  | `PART_DockPreview`, `GripLine` | `Rectangle` |
  | `PART_Icon` | `FontIcon` |

  The remaining parts are the library's own types (`ToolWindowTitleBar`, `DockGuide`, `DockGuidePanel`,
  `AutoHideTabStrip`, ...) and need nothing.

- **Your own code.** Any `as`, `is` or explicit cast to a WinUI class on an object that native code
  returned (`GetTemplateChild`, `VisualTreeHelper`, `FindName`, `OriginalSource`) is exposed to the
  same thing. Casts to `FrameworkElement`, `UIElement` and `Control` are not.

To keep a class, put this on a member that is reachable from the code that does the cast:

```csharp
[DynamicDependency(
    DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.PublicFields,
    typeof(WrapPanel))]
protected override void OnApplyTemplate() { ... }
```

CsWinRT's `[DynamicWindowsRuntimeCast(typeof(T))]` does the same where your target framework has it,
and its analyzer (enabled with `CsWinRTAotWarningLevel` 3) finds the casts that need it.

To check what you got, walk the visual tree with `VisualTreeHelper` and print `GetType()` for each
element. A removed class shows up as `FrameworkElement`, `ContentControl` or `Panel` where the
template declares a `ContentPresenter`, `Button` or `StackPanel`.
