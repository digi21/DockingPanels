# Digi21.WinUI.Docking

[![CI](https://github.com/Digi21/DockingPanels/actions/workflows/ci.yml/badge.svg)](https://github.com/Digi21/DockingPanels/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Digi21.WinUI.Docking.svg)](https://www.nuget.org/packages/Digi21.WinUI.Docking)
[![NuGet downloads](https://img.shields.io/nuget/dt/Digi21.WinUI.Docking.svg)](https://www.nuget.org/packages/Digi21.WinUI.Docking)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Docking panels for WinUI 3 applications: dockable tool windows with splitters and tabs, Visual Studio-style drag-and-drop dock guides, floating windows, auto-hide, and layout serialization.

> ⚠️ **Under active development.** The first release (`v0.1.0`) has not been published yet. The API may change until then.

## Features

- `DockSite` root control hosting a declarative docking layout.
- `ToolWindow` panels dockable to any side of the workspace or of each other, by code or by dragging.
- Proportional resizing with splitters (`SplitContainer` + `DockSite.RelativeSize`).
- Multiple tool windows in one container become tabs; switching tabs preserves control state.
- Drag & drop re-docking with Visual Studio-style dock guides and drop previews.
- Floating tool windows in real top-level windows, across monitors.
- Docking *inside* a floating window: it takes drops with its own dock guides and holds a
  layout of split panes and tabs, like a small dock site.
- Auto-hide (unpin) tool windows to the dock site edges, with a slide-in flyout.
- Save and restore the docking layout as XML (`DockSiteLayoutSerializer`), including
  auto-hidden groups, floating window positions and their inner layout.
- Cancelable close, activation tracking, and layout-change events on `DockSite`.
- Light, dark, and high-contrast aware out of the box (built on WinUI theme resources).

### Roadmap (not yet implemented)

- Tabbed MDI document area.

## Requirements

- Windows App SDK 1.8 or later.
- .NET 8 or later.
- Windows 10 version 1809 (build 17763) or later.

## Installation

```
dotnet add package Digi21.WinUI.Docking
```

## Quickstart

```xml
<Window
    x:Class="MyApp.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:docking="using:Digi21.WinUI.Docking">

  <docking:DockSite x:Name="DockSite">
    <docking:SplitContainer Orientation="Horizontal">

      <docking:ToolWindowContainer docking:DockSite.RelativeSize="0.25">
        <docking:ToolWindow Title="Solution Explorer" SerializationId="solutionExplorer">
          <TreeView />
        </docking:ToolWindow>
        <docking:ToolWindow Title="Class View" SerializationId="classView">
          <ListView />
        </docking:ToolWindow>
      </docking:ToolWindowContainer>

      <docking:SplitContainer docking:DockSite.RelativeSize="0.75" Orientation="Vertical">
        <docking:Workspace docking:DockSite.RelativeSize="0.7">
          <TextBox AcceptsReturn="True" />
        </docking:Workspace>
        <docking:ToolWindowContainer docking:DockSite.RelativeSize="0.3">
          <docking:ToolWindow Title="Output" SerializationId="output">
            <TextBlock Text="Build succeeded." />
          </docking:ToolWindow>
        </docking:ToolWindowContainer>
      </docking:SplitContainer>

    </docking:SplitContainer>
  </docking:DockSite>

</Window>
```

Windows in the same `ToolWindowContainer` become tabs. Users can re-dock any window by
dragging its tab or title bar: dock guides appear over the hovered target (dock to any side,
or drop on the center guide to attach as a tab) and at the edges of the whole dock site.

### Floating windows

Dragging a tool window out of the layout and dropping it away from every dock guide floats it
into its own top-level window, which can be moved to any monitor. Floating windows are owned by
the application window, so they stay above it, stay out of the taskbar, and close with it.
Dragging their caption back over the dock site shows the same dock guides as any other drag.
Double-clicking a title bar floats a docked window and docks a floating one back where it came
from.

A floating window is a docking surface of its own: dragging a window over it shows the same dock
guides, so the drop can split it into panes or attach the window as a tab of one of them. With a
single pane, the pane's title bar is the window's caption; once it holds several panes, the
window gets a caption of its own, which drags and docks the whole group as before, while each
pane's title bar drags only that pane.

```csharp
outputWindow.Float();                       // float it near the dock site
dockSite.FloatToolWindow(outputWindow);     // same, from the dock site
dockSite.FloatToolWindow(outputWindow, new RectInt32(2200, 300, 480, 640));  // explicit screen bounds

outputWindow.Dock();                        // back to the position it was floated from
```

### Auto-hide

```csharp
outputWindow.AutoHide();   // collapse the whole pane to its nearest edge
outputWindow.Dock();       // pin it back where it was
```

Unpinned windows become tabs on the dock site edge; clicking a tab slides the window over the
layout until it loses focus. Set `CanAutoHide="False"` (or `CanFloat="False"`) on a tool window
to hide the affordance and block the operation.

### Programmatic docking

```csharp
// Dock to an edge of the whole dock site (also reopens closed windows).
dockSite.DockToolWindow(outputWindow, DockSide.Bottom);

// Dock beside another window's container.
dockSite.DockToolWindow(outputWindow, solutionExplorer, DockSide.Right);

// Attach as a tab next to another window.
dockSite.AttachToolWindow(outputWindow, solutionExplorer);

outputWindow.Activate();
outputWindow.Close();   // cancelable via DockSite.WindowClosing
```

### Saving and restoring the layout

Give every tool window a stable `SerializationId`, then:

```csharp
var serializer = new DockSiteLayoutSerializer();

string xml = serializer.SaveToString(dockSite);   // or SaveToFile / SaveToStream

serializer.ToolWindowResolving += (_, e) =>
{
    // Optional: create windows on demand for ids that are not registered yet.
    e.ToolWindow = CreateToolWindow(e.Id);
};
serializer.LoadFromString(dockSite, xml);         // or LoadFromFile / LoadFromStream
```

Only the structure is saved (splits, proportions, tab order, selection, auto-hidden groups, and
the screen bounds of floating windows together with the layout inside them). Window instances and
their content are matched by id and reused, so control state survives a reload. Floating windows
are restored on a monitor that exists, so a layout saved with two monitors still loads on one.
Layouts written by earlier versions are still read.

## Sample

The [`samples/DockingGallery`](samples/DockingGallery) app in this repository demonstrates all
features and is the easiest way to try the library: clone the repository and run

```
dotnet build
dotnet run --project samples/DockingGallery
```

## License

[MIT](LICENSE)
