# Theming

The docking chrome is painted with brushes of its own, every one of them aliased to a WinUI
system brush. Out of the box it follows the light, dark and high-contrast themes with no setup at
all. To recolor it, redefine the keys below in your application's resources — retemplating is only
needed to change the *shape* of a control, not its colors.

## Overriding the colors

Put the overrides in `App.xaml`, inside a `ResourceDictionary` **merged** into
`Application.Resources`:

```xml
<Application
    x:Class="MyApp.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />

        <ResourceDictionary>
          <ResourceDictionary.ThemeDictionaries>
            <ResourceDictionary x:Key="Default">
              <SolidColorBrush x:Key="DockingPaneBackgroundBrush" Color="#102A43" />
              <SolidColorBrush x:Key="DockingTitleBarActiveBackgroundBrush" Color="#C50F1F" />
            </ResourceDictionary>
            <ResourceDictionary x:Key="Light">
              <SolidColorBrush x:Key="DockingPaneBackgroundBrush" Color="#FFF4E5" />
              <SolidColorBrush x:Key="DockingTitleBarActiveBackgroundBrush" Color="#B4009E" />
            </ResourceDictionary>
          </ResourceDictionary.ThemeDictionaries>
        </ResourceDictionary>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

Two things about that shape are not obvious, and both will silently do nothing if you get them
wrong:

- **The theme dictionaries have to be inside a merged dictionary.** WinUI ignores
  `ThemeDictionaries` declared directly on `Application.Resources`. The extra nested
  `<ResourceDictionary>` above is what makes them take effect.
- **The overrides have to be application-wide.** Floating windows are real top-level windows with
  a visual tree of their own, so resources declared on an element (or on a page) never reach them.
  They do follow the dock site's theme: setting `RequestedTheme` anywhere above the `DockSite`
  switches the floating windows along with it.

If a color is the same in every theme, a single entry declared directly in `Application.Resources`
(no theme dictionaries at all) works too.

## Brushes

### Surfaces

| Key | Defaults to | Paints |
| --- | --- | --- |
| `DockingSiteBackgroundBrush` | `LayerFillColorDefaultBrush` | Behind the whole `DockSite` |
| `DockingFloatingWindowBackgroundBrush` | `LayerFillColorDefaultBrush` | Behind a floating window |
| `DockingPaneBackgroundBrush` | `CardBackgroundFillColorDefaultBrush` | `Workspace`, tool window and document panes |
| `DockingPaneBorderBrush` | `CardStrokeColorDefaultBrush` | Their border |
| `DockingDocumentHostBackgroundBrush` | `SolidBackgroundFillColorTertiaryBrush` | Behind the document area |
| `DockingTabStripBackgroundBrush` | `LayerFillColorDefaultBrush` | The document tab strip |
| `DockingTabStripBorderBrush` | `DividerStrokeColorDefaultBrush` | The rule under it |

### Tool window title bar

| Key | Defaults to |
| --- | --- |
| `DockingTitleBarBackgroundBrush` | `SubtleFillColorTransparentBrush` |
| `DockingTitleBarForegroundBrush` | `TextFillColorPrimaryBrush` |
| `DockingTitleBarActiveBackgroundBrush` | `AccentFillColorDefaultBrush` |
| `DockingTitleBarActiveForegroundBrush` | `TextOnAccentFillColorPrimaryBrush` |

The *active* pair is what marks the pane holding the active window.

### Floating window caption

| Key | Defaults to |
| --- | --- |
| `DockingCaptionBackgroundBrush` | `AccentFillColorDefaultBrush` |
| `DockingCaptionForegroundBrush` | `TextOnAccentFillColorPrimaryBrush` |

### Tabs

| Key | Defaults to | Applies to |
| --- | --- | --- |
| `DockingTabBackgroundBrush` | `SubtleFillColorTransparentBrush` | Both tool and document tabs |
| `DockingTabForegroundBrush` | `TextFillColorPrimaryBrush` | Both |
| `DockingTabPointerOverBackgroundBrush` | `SubtleFillColorSecondaryBrush` | Tool window tabs |
| `DockingTabPressedBackgroundBrush` | `SubtleFillColorTertiaryBrush` | Tool window tabs |
| `DockingTabSelectedForegroundBrush` | `AccentTextFillColorPrimaryBrush` | Selected tool window tab |
| `DockingTabSelectionIndicatorBrush` | `AccentFillColorDefaultBrush` | Its underline |
| `DockingDocumentTabSelectedBackgroundBrush` | `CardBackgroundFillColorDefaultBrush` | Selected document tab |
| `DockingDocumentTabActiveBackgroundBrush` | `AccentFillColorDefaultBrush` | Document tab holding the active window |
| `DockingDocumentTabActiveForegroundBrush` | `TextOnAccentFillColorPrimaryBrush` | Its text and close button |

### Splitters

| Key | Defaults to |
| --- | --- |
| `DockingSplitterBrush` | `DividerStrokeColorDefaultBrush` |
| `DockingSplitterPointerOverBrush` | `AccentFillColorDefaultBrush` |
| `DockingSplitterPressedBrush` | `AccentFillColorTertiaryBrush` |

### Dock guides and drag feedback

| Key | Defaults to |
| --- | --- |
| `DockingGuideBackgroundBrush` | `CardBackgroundFillColorDefaultBrush` |
| `DockingGuideBorderBrush` | `CardStrokeColorDefaultBrush` |
| `DockingGuideForegroundBrush` | `TextFillColorPrimaryBrush` |
| `DockingGuideHotBackgroundBrush` | `AccentFillColorDefaultBrush` |
| `DockingGuideHotForegroundBrush` | `TextOnAccentFillColorPrimaryBrush` |
| `DockingDropPreviewFillBrush` | `AccentFillColorDefaultBrush` |
| `DockingDropPreviewStrokeBrush` | `AccentFillColorDefaultBrush` |
| `DockingDragGhostBackgroundBrush` | `CardBackgroundFillColorDefaultBrush` |
| `DockingDragGhostBorderBrush` | `AccentFillColorDefaultBrush` |
| `DockingDragGhostForegroundBrush` | `TextFillColorPrimaryBrush` |

The drag ghost is only shown for windows that cannot be torn off into a floating window
(`CanFloat="False"`); every other drag carries the real window.

### Auto-hide

| Key | Defaults to |
| --- | --- |
| `DockingAutoHideTabStripBackgroundBrush` | `CardBackgroundFillColorDefaultBrush` |
| `DockingAutoHideTabBackgroundBrush` | `SubtleFillColorTransparentBrush` |
| `DockingAutoHideTabBorderBrush` | `CardStrokeColorDefaultBrush` |
| `DockingAutoHideTabForegroundBrush` | `TextFillColorPrimaryBrush` |
| `DockingFlyoutBackgroundBrush` | `SolidBackgroundFillColorSecondaryBrush` |
| `DockingFlyoutBorderBrush` | `SurfaceStrokeColorDefaultBrush` |

## Metrics

These are the same in every theme, so declare them directly in `Application.Resources`:

```xml
<x:Double x:Key="DockingTitleBarHeight">28</x:Double>
```

| Key | Type | Default |
| --- | --- | --- |
| `DockingTitleBarHeight` | `x:Double` | `32` |
| `DockingCaptionHeight` | `x:Double` | `32` |
| `DockingDocumentTabHeight` | `x:Double` | `30` |
| `DockingToolWindowTabHeight` | `x:Double` | `28` |
| `DockingGuideSize` | `x:Double` | `40` |
| `DockingGuideClusterSize` | `x:Double` | `128` |
| `DockingDropPreviewOpacity` | `x:Double` | `0.3` |
| `DockingSplitterThickness` | `x:Double` | `6` |
| `DockingSplitterGripThickness` | `x:Double` | `1` |
| `DockingPaneBorderThickness` | `Thickness` | `1` |
| `DockingPaneCornerRadius` | `CornerRadius` | `0` |
| `DockingDocumentTabCornerRadius` | `CornerRadius` | `4,4,0,0` |
| `DockingGuideCornerRadius` | `CornerRadius` | `4` |
| `DockingFlyoutCornerRadius` | `CornerRadius` | `4` |

`DockingGuideSize` and `DockingGuideClusterSize` are safe to change: the drag code measures the
guides it actually drew instead of assuming a size, so the clickable areas follow whatever the
guides end up being.

`DockingSplitterThickness` is how much room a splitter takes between two panes — its whole
draggable band — and `DockingSplitterGripThickness` is the line drawn inside it. Both are read
from the resources by the layout code, because a splitter is sized by the panel that arranges it
rather than by a template of its own. That also means they are resolved once, on the first layout
pass, and are not re-read afterwards.

`DockingPaneCornerRadius` is the one to use for rounded panes. Setting `CornerRadius` on a
container in XAML works too, but only for the containers you declared: the panes the user creates
by dragging a window out and splitting are built in code and take the default.

## Icons

The close and pin buttons, and the arrows inside the dock guides, are glyphs of the icon font.
Replacing the font means replacing the glyphs with the code points of the new one:

```xml
<FontFamily x:Key="DockingIconFontFamily">My Icon Font</FontFamily>
<x:String x:Key="DockingCloseGlyph">&#xE711;</x:String>
```

| Key | Type | Default |
| --- | --- | --- |
| `DockingIconFontFamily` | `FontFamily` | `SymbolThemeFontFamily` |
| `DockingIconFontSize` | `x:Double` | `10` |
| `DockingGuideIconFontSize` | `x:Double` | `14` |
| `DockingCloseGlyph` | `x:String` | `&#xE8BB;` |
| `DockingPinGlyph` | `x:String` | `&#xE718;` |
| `DockingGuideCenterGlyph` | `x:String` | `&#xE8A9;` |
| `DockingGuideLeftGlyph` | `x:String` | `&#xE76B;` |
| `DockingGuideTopGlyph` | `x:String` | `&#xE70E;` |
| `DockingGuideRightGlyph` | `x:String` | `&#xE76C;` |
| `DockingGuideBottomGlyph` | `x:String` | `&#xE70D;` |

The guide glyphs are chosen in code, since which one is drawn depends on the side the guide stands
for, so they are read from the resources the same way the splitter metrics are. Anything richer
than a glyph — a `PathIcon`, an image — needs a retemplate.

## Text

Two styles cover every piece of text the chrome draws. Both default to WinUI's
`CaptionTextBlockStyle`; overriding *that* would restyle the whole application, which is why they
exist:

| Key | Type | Applies to |
| --- | --- | --- |
| `DockingTitleTextStyle` | `Style` (`TextBlock`) | Tool window title bars, floating window captions, the drag ghost |
| `DockingTabTextStyle` | `Style` (`TextBlock`) | Document, tool window and auto-hide tabs |

```xml
<Style x:Key="DockingTabTextStyle" TargetType="TextBlock" BasedOn="{StaticResource BodyStrongTextBlockStyle}" />
```

## Retemplating

Everything above only changes colors and sizes. To change the structure of a control, replace its
`ControlTemplate` with a style of your own. To keep the rest of the default style, derive from it
with `BasedOn` — which requires merging the library's `Generic.xaml`, since that is where the
default styles live:

```xml
<ResourceDictionary.MergedDictionaries>
  <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
  <ResourceDictionary Source="ms-appx:///Digi21.WinUI.Docking/Themes/Generic.xaml" />
</ResourceDictionary.MergedDictionaries>
```

```xml
<Style
    x:Key="CompactToolWindowContainerStyle"
    BasedOn="{StaticResource DefaultToolWindowContainerStyle}"
    TargetType="docking:ToolWindowContainer">
  <Setter Property="BorderThickness" Value="0" />
</Style>
```

Without that merge the `BasedOn` reference cannot be resolved and the application crashes when the
style is first used, so merge it whenever you derive from a default style. Every default style is
keyed `Default<ControlName>Style`: `DefaultDockSiteStyle`, `DefaultWorkspaceStyle`,
`DefaultToolWindowContainerStyle`, `DefaultDocumentContainerStyle`, `DefaultDocumentTabItemStyle`,
`DefaultToolWindowTitleBarStyle`, `DefaultToolWindowTabItemStyle`, `DefaultFloatingWindowRootStyle`,
`DefaultFloatingWindowCaptionStyle`, `DefaultDockGuideStyle`, `DefaultDockGuidePanelStyle`,
`DefaultDockSplitterStyle`, `DefaultAutoHideTabStripStyle`, `DefaultAutoHideTabItemStyle`,
`DefaultAutoHideFlyoutStyle`.

A retemplated control must keep the `PART_` named parts of the original template: they are how the
code finds the title, the close button, the tab strip and the layout host.

## How it works

The keys live in `Themes/DockingResources.xaml`, which the library merges into
`Application.Resources` the first time a docking control is created — at the bottom of the merged
dictionary collection, so everything the application declares wins over them. They are deliberately
*not* declared next to the templates in `Themes/Generic.xaml`: a key found in the same dictionary
as the template that uses it takes precedence over the application's resources, which would make
these keys impossible to override.

Most of them are read by the templates. The few the code reads instead — the splitter metrics and
the guide glyphs — are values no template can supply, because they depend on how a control is
arranged or on which side it stands for. Those are looked up in `Application.Resources`, so unlike
the rest they cannot be overridden on an element; and being theme-independent, they live in the
root of the dictionary rather than in its theme dictionaries.
