# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Document tabs can be pinned, as in Visual Studio. A pinned tab keeps its own block at the head of
  its group, in its own order, outside the part of the strip that scrolls, so it stays in view
  however many documents are open. `DocumentWindow.IsPinned`, `Pin()` and `Unpin()` are the API;
  the tab's pin button and its new context menu are the gesture. Dragging never moves a tab from
  one block to the other.
- `DocumentHost.CloseDocuments(DocumentCloseScope)` closes the documents of an area in one go —
  `All`, `AllButPinned` or `AllButActive` — which is what pinned tabs survive. Each document is
  closed on its own, so `CanClose` and a canceled `DockSite.WindowClosing` still hold.
- A provisional (preview) document tab, the other half of Visual Studio's tab behaviour: one per
  group, at the end of the strip, drawn in italics, and replaced by the next preview instead of
  leaving a tab behind. `DocumentWindow.IsProvisional` and `KeepOpen()` are the API,
  `OpenDocument(document, provisional: true)` opens one, and double-clicking the tab, dragging it,
  pinning it or "keep open" promote it. Editing the document promotes it too, from one line in the
  application: the content belongs to the host, so it is the host that calls `KeepOpen()`.
- `DockSite.DocumentTabContextMenuOpening`, raised with the entries of a document tab's context
  menu before it opens, for an application to add its own commands or replace the menu.
- Theme keys for the new chrome: `DockingTabPinGlyph`, `DockingTabUnpinGlyph`,
  `DockingPinnedTabStripMaxWidth`, `DockingProvisionalTabStripMaxWidth`, `DockingPinTabButtonName`,
  `DockingUnpinTabButtonName`, `DockingKeepTabOpenName`, `DockingCloseAllTabsName`,
  `DockingCloseAllButPinnedTabsName` and `DockingCloseAllButThisTabName`. The existing
  `DockingPinGlyph` / `DockingUnpinGlyph` keep meaning a tool window's auto-hide button.

### Changed

- Layouts record which document tabs are pinned and which one is provisional, in new `IsPinned` and
  `IsProvisional` attributes written only for the tabs that are. The format version does not move: a
  layout with neither is what 1.1 wrote, and 1.1 reads one that has them, ignoring the attributes.
- Only the primary pointer button starts a window drag, so the secondary one reaches the tab's
  context menu.

## [1.1.1] - 2026-08-15

### Fixed

- Two panels auto-hidden to the same edge no longer draw their tabs on top of each other. Each
  group still lands where its panel used to be, but a group that would cover another is pushed
  past it, and a run of tabs longer than the edge is pulled back inside it.

## [1.1.0] - 2026-08-15

### Added

- Pointing at an auto-hide tab now previews its window, as in Visual Studio. The preview is not
  activated and slides back when the pointer leaves it.
- `DockSite.AutoHideCloseDelay`, the grace period before a previewed panel slides back once the
  pointer leaves. Defaults to 350 ms, which covers the pointer crossing outside the panel on its
  way to a control near the edge.
- `CanAutoHide` is saved in the layout, so an application can settle from its layout file which
  panels stay docked instead of relying on XAML or on the user leaving the pin alone. Layouts
  written before this release do not carry the attribute and load exactly as they did, and the
  attribute is only written for windows that forbid auto-hiding, so 1.0.0 still reads what this
  version saves.
- Accessible names on the chrome: the pin button (which says whether it will auto-hide or dock),
  the close buttons of tool window title bars, document tabs and floating captions, and the tab of
  every window. They are resource keys — `DockingCloseButtonName`, `DockingAutoHideButtonName`,
  `DockingDockButtonName` — so an application that is not in English redefines them with the rest
  of the theme, and `DockingUnpinGlyph` joins `DockingPinGlyph` as a themable glyph.

### Changed

- An open auto-hide panel now closes when the focus leaves it, not on any click elsewhere in the
  dock site. A panel opened by clicking its tab, or holding the focus, stays put through clicks
  that take no focus with them — empty chrome, a splitter, a control that refuses focus — and only
  a preview still goes away with the pointer.

### Fixed

- A tab group holding a window with `CanAutoHide="False"` can no longer be sent to an edge through
  one of its neighbours, which used to take the whole group with it. The pin button is hidden for
  the whole group in that case rather than being shown and doing nothing.
- Loading a layout that collapsed a window to an edge no longer strands it there when the window
  now forbids auto-hiding: with auto-hide off there is no pin button to bring it back, so it is
  docked instead. It is docked where the file says the group came from — beside the same neighbour,
  at the same relative size — which is what pinning it from the edge would have done, instead of
  landing at the left of the layout; when that neighbour is gone it goes to the group's own edge.
- `DockingPinGlyph` is honored again on a title bar whose window changed state; the glyph was
  being overwritten from code with a hard-coded code point.

## [1.0.0] - 2026-08-11

First release. The API is stable: it took its final shape while the library was being built against
a real application, and the private `0.1.0-dev.*` packages were where it changed.

### Added

#### Layout

- `DockSite`, the root control of a docking layout, hosting a tree of containers declared in XAML.
- `SplitContainer` panes with draggable splitters and proportional sizing through the
  `DockSite.RelativeSize` attached property.
- `ToolWindow` panels hosted in `ToolWindowContainer` panes, which show a title bar and become
  tabs when the pane holds several windows. Every hosted window stays loaded, so switching tabs
  preserves control state.
- `Workspace`, a plain central content area for applications that do not use documents.

#### Documents (tabbed MDI)

- `DocumentHost`, the central document area, holding a tree of `DocumentContainer` tab groups that
  the user splits by dropping documents on the side guides.
- `DocumentWindow` documents, with tabs along the top of their group, close buttons, and
  reordering by dragging a tab along its strip.
- `DockSite.OpenDocument`, `DockSite.ActiveDocument`, `DockSite.Documents`, and
  `DocumentHost.Groups` / `Documents` / `ActiveGroup` / `OpenDocument`.

#### Dragging and docking

- Drag-and-drop re-docking with Visual Studio-style dock guides and drop previews, over the dock
  site and over every floating window.
- Windows are torn off into a real floating window as soon as the drag leaves their tab strip, so
  the drag shows the window with its live content instead of a placeholder.
- Documents dock only inside the document area, and tool windows only around it.
- Programmatic docking: `DockSite.DockToolWindow`, `AttachToolWindow` and `FloatWindow`, plus
  `DockingWindow.Activate` / `Float` / `Dock` / `Close`.

#### Floating windows

- Tool windows and documents float into real top-level windows that can be moved across monitors.
  They are owned by the application window, so they stay above it, stay out of the taskbar, and
  close with it.
- A floating window is a docking surface of its own: windows dropped on it split it into panes or
  join one of its panes as tabs.
- `DockSite.CloseFloatingWindows()`, for applications that close their window from code. The dock
  site closes them by itself when the user closes the window; WinUI raises nothing a control can
  reach in time when the application calls `Window.Close()`, and floating windows destroyed with
  their owner take the process down. Call it from the window's `Closed` handler.

#### Auto-hide

- `ToolWindow.AutoHide()` collapses a pane to the nearest dock site edge and `Dock()` pins it back
  where it was; unpinned windows become tabs on the edge, with a slide-in flyout.
- `CanClose`, `CanDragWindow`, `CanFloat` and `CanAutoHide` gate the corresponding affordances.

#### Serialization

- `DockSiteLayoutSerializer` saves and restores the layout as versioned XML (format version 3) from
  and to a string, a file or a stream, including the document area, the auto-hide groups, and the
  floating windows together with the layout inside them.
- Windows are matched by `SerializationId` and reused, so their content and state survive a load.
  `ToolWindowResolving` and `DocumentResolving` create windows on demand, and
  `UnresolvedWindowBehavior` decides what happens to open windows the loaded layout does not
  mention.
- Floating windows are restored on a monitor that exists, so a layout saved on a multi-monitor
  setup still loads on a single one.

#### Events

- `DockSite.WindowOpened`, `WindowClosing` (cancelable), `WindowClosed`, `WindowActivated`,
  `WindowDeactivated` and `LayoutChanged`.
- `Relocated` on `Workspace`, `DocumentHost`, `ToolWindow` and `DocumentWindow`, raised once the
  XAML tree has settled after a docking operation has moved the element. It is what content with a
  life cycle of its own (a `SwapChainPanel`, a `WebView2`, a render loop) should hang off:
  WinUI raises `Loaded` before `Unloaded` for an element that was moved but never left the tree,
  so the unload notification arrives last and stops such content for good.

#### Presentation

- Light, dark and high-contrast aware out of the box, built on WinUI theme resources; floating
  windows follow the theme of the dock site.
- A named brush and metric for every part of the docking chrome (`DockingPaneBackgroundBrush`,
  `DockingTitleBarActiveBackgroundBrush`, `DockingGuideSize`, …), so an application recolors or
  resizes it by redefining those keys instead of retemplating. See [docs/theming.md](docs/theming.md).
- The same for the icon glyphs and font (`DockingCloseGlyph`, `DockingPinGlyph`, the four guide
  arrows, `DockingIconFontFamily`), the text styles (`DockingTitleTextStyle`, `DockingTabTextStyle`),
  the pane corner radius (`DockingPaneCornerRadius`) and the splitter metrics
  (`DockingSplitterThickness`, `DockingSplitterGripThickness`).

#### Sample

- The `DockingGallery` app carries an **Event Trace** panel: `Loaded`, `Unloaded`, `Relocated`,
  `LayoutChanged` and the open/close notifications in the order they actually happen, which is the
  order that matters when hosting content with a life cycle of its own.

### Requirements

- Windows App SDK 1.8 or later, .NET 8 or later, and Windows 10 version 1809 (build 17763) or
  later.

[1.1.1]: https://github.com/Digi21/DockingPanels/releases/tag/v1.1.1
[1.1.0]: https://github.com/Digi21/DockingPanels/releases/tag/v1.1.0
[1.0.0]: https://github.com/Digi21/DockingPanels/releases/tag/v1.0.0
