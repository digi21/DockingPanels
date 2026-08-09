# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Until `v0.1.0`
is published, the entries below describe what that first release will contain; prerelease
`0.1.0-dev.*` packages are tracked here as their API changes.

## [Unreleased]

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

### Requirements

- Windows App SDK 1.8 or later, .NET 8 or later, and Windows 10 version 1809 (build 17763) or
  later.

[Unreleased]: https://github.com/Digi21/DockingPanels/commits/main
