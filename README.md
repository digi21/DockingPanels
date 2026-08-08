# Digi21.WinUI.Docking

Docking panels for WinUI 3 applications: dockable tool windows with splitters and tabs, Visual Studio-style drag-and-drop dock guides, and layout serialization.

> ⚠️ **Under active development.** The first release (`v0.1.0`) has not been published yet. The API may change until then.

## Features

- `DockSite` root control hosting a declarative docking layout.
- `ToolWindow` panels dockable to any side of the workspace or of each other.
- Proportional resizing with splitters.
- Multiple tool windows in one container become tabs.
- Drag & drop re-docking with Visual Studio-style dock guides.
- Save and restore the docking layout (XML).
- Light, dark, and high-contrast theme support out of the box.

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

  <docking:DockSite>
    <docking:SplitContainer Orientation="Horizontal">

      <docking:ToolWindowContainer docking:DockSite.RelativeSize="0.25">
        <docking:ToolWindow Title="Solution Explorer" SerializationId="solutionExplorer">
          <TreeView />
        </docking:ToolWindow>
      </docking:ToolWindowContainer>

      <docking:Workspace docking:DockSite.RelativeSize="0.75">
        <TextBox AcceptsReturn="True" />
      </docking:Workspace>

    </docking:SplitContainer>
  </docking:DockSite>

</Window>
```

## License

[MIT](LICENSE)
