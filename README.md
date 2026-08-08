# Digi21.WinUI.Docking

[![CI](https://github.com/Digi21/DockingPanels/actions/workflows/ci.yml/badge.svg)](https://github.com/Digi21/DockingPanels/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Digi21.WinUI.Docking.svg)](https://www.nuget.org/packages/Digi21.WinUI.Docking)
[![NuGet downloads](https://img.shields.io/nuget/dt/Digi21.WinUI.Docking.svg)](https://www.nuget.org/packages/Digi21.WinUI.Docking)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Docking panels for WinUI 3 applications: dockable tool windows with splitters and tabs, Visual Studio-style drag-and-drop dock guides, and layout serialization.

> ⚠️ **Under active development.** The first release (`v0.1.0`) has not been published yet. The API may change until then.

## Features

- `DockSite` root control hosting a declarative docking layout.
- `ToolWindow` panels dockable to any side of the workspace or of each other, by code or by dragging.
- Proportional resizing with splitters (`SplitContainer` + `DockSite.RelativeSize`).
- Multiple tool windows in one container become tabs; switching tabs preserves control state.
- Drag & drop re-docking with Visual Studio-style dock guides and drop previews.
- Save and restore the docking layout as XML (`DockSiteLayoutSerializer`).
- Cancelable close, activation tracking, and layout-change events on `DockSite`.
- Light, dark, and high-contrast aware out of the box (built on WinUI theme resources).

### Roadmap (not yet implemented)

- Floating windows (multi-monitor).
- Auto-hide tool windows.
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

Only the structure is saved (splits, proportions, tab order, selection). Window instances and
their content are matched by id and reused, so control state survives a reload.

## Sample

The [`samples/DockingGallery`](samples/DockingGallery) app in this repository demonstrates all
features and is the easiest way to try the library: clone the repository and run

```
dotnet build
dotnet run --project samples/DockingGallery
```

## License

[MIT](LICENSE)
