using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Digi21.WinUI.Docking.Serialization;

/// <summary>
/// Saves and restores the docking layout of a <see cref="DockSite"/> as versioned XML.
/// Only the structure is persisted (containers, splits, proportions, tab order and selection);
/// tool window instances and their content are matched by <see cref="DockingWindow.SerializationId"/>
/// and reused, so control state survives a layout reload.
/// </summary>
public class DockSiteLayoutSerializer
{
    /// <summary>
    /// Raised while loading when a serialized window id does not match any registered window,
    /// giving the application a chance to create it on demand.
    /// </summary>
    public event EventHandler<ToolWindowResolvingEventArgs>? ToolWindowResolving;

    /// <summary>
    /// Gets or sets what happens to windows that are open but absent from the loaded layout.
    /// The default is <see cref="UnresolvedWindowBehavior.Close"/>.
    /// </summary>
    public UnresolvedWindowBehavior UnresolvedWindowBehavior { get; set; } = UnresolvedWindowBehavior.Close;

    /// <summary>Saves the layout of the given dock site as an XML string.</summary>
    /// <param name="dockSite">The dock site whose layout is saved.</param>
    public string SaveToString(DockSite dockSite)
    {
        using var stream = new MemoryStream();
        SaveToStream(dockSite, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Saves the layout of the given dock site to a file.</summary>
    /// <param name="dockSite">The dock site whose layout is saved.</param>
    /// <param name="path">The file to write.</param>
    public void SaveToFile(DockSite dockSite, string path)
    {
        using var stream = File.Create(path);
        SaveToStream(dockSite, stream);
    }

    /// <summary>Saves the layout of the given dock site to a stream.</summary>
    /// <param name="dockSite">The dock site whose layout is saved.</param>
    /// <param name="stream">The stream to write to.</param>
    public void SaveToStream(DockSite dockSite, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(dockSite);
        ArgumentNullException.ThrowIfNull(stream);

        var layout = new LayoutDocument { Root = dockSite.Child is null ? null : Capture(dockSite.Child) };

        foreach (var group in dockSite.AutoHideGroups)
        {
            var groupNode = new AutoHideGroupNode { Edge = group.Edge, Size = group.Size, Offset = group.Offset };

            if (group.RestoreHint is { } hint && CaptureSiblingReference(dockSite, hint.Sibling) is { } siblingReference)
            {
                groupNode.RestoreSibling = siblingReference;
                groupNode.RestoreSide = hint.Side;
                groupNode.RestoreRelativeSize = hint.RelativeSize;
            }

            CaptureWindows(group.Windows, groupNode.Windows);
            layout.AutoHideGroups.Add(groupNode);
        }

        foreach (var host in dockSite.FloatingHosts)
        {
            if (host.LayoutChild is not { } hostRoot)
            {
                continue;
            }

            var bounds = host.Bounds;
            var floatingNode = new FloatingWindowNode
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height,
                RestoreContainer = CaptureSiblingReference(dockSite, host.RestoreHint?.Container),

                // A floating window holds a layout tree of its own: windows can be docked and
                // split inside it, and that structure is part of the layout.
                Root = Capture(hostRoot),
            };

            if (host.RestoreHint is { } hint && CaptureSiblingReference(dockSite, hint.Sibling) is { } siblingReference)
            {
                floatingNode.RestoreSibling = siblingReference;
                floatingNode.RestoreSide = hint.Side;
                floatingNode.RestoreRelativeSize = hint.RelativeSize;
            }

            layout.FloatingWindows.Add(floatingNode);
        }

        LayoutXml.Write(stream, layout);

        static void CaptureWindows(IEnumerable<ToolWindow> windows, List<LayoutWindowEntry> entries)
        {
            foreach (var window in windows)
            {
                var id = window.SerializationId
                    ?? throw new InvalidOperationException(
                        $"The tool window '{window.Title}' has no SerializationId. Every open tool window needs a stable id to save the layout.");
                entries.Add(new LayoutWindowEntry(id, window.State.ToString()));
            }
        }
    }

    /// <summary>Loads a layout previously saved with this serializer from an XML string.</summary>
    /// <param name="dockSite">The dock site whose layout is replaced.</param>
    /// <param name="xml">The XML produced by a save method.</param>
    public void LoadFromString(DockSite dockSite, string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        LoadFromStream(dockSite, stream);
    }

    /// <summary>Loads a layout from a file.</summary>
    /// <param name="dockSite">The dock site whose layout is replaced.</param>
    /// <param name="path">The file to read.</param>
    public void LoadFromFile(DockSite dockSite, string path)
    {
        using var stream = File.OpenRead(path);
        LoadFromStream(dockSite, stream);
    }

    /// <summary>Loads a layout from a stream.</summary>
    /// <param name="dockSite">The dock site whose layout is replaced.</param>
    /// <param name="stream">The stream to read from.</param>
    public void LoadFromStream(DockSite dockSite, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(dockSite);
        ArgumentNullException.ThrowIfNull(stream);

        var layout = LayoutXml.Read(stream);
        Apply(dockSite, layout);
    }

    private static LayoutNode Capture(UIElement element)
    {
        switch (element)
        {
            case SplitContainer split:
                var splitNode = new SplitLayoutNode
                {
                    Orientation = split.Orientation,
                    RelativeSize = DockSite.GetRelativeSize(split),
                };
                foreach (var pane in split.GetPanes())
                {
                    splitNode.Children.Add(Capture(pane));
                }

                return splitNode;

            case ToolWindowContainer container:
                var containerNode = new ContainerLayoutNode
                {
                    RelativeSize = DockSite.GetRelativeSize(container),
                    SelectedId = container.SelectedItem?.SerializationId,
                };
                foreach (var window in container.Items)
                {
                    var id = window.SerializationId
                        ?? throw new InvalidOperationException(
                            $"The tool window '{window.Title}' has no SerializationId. Every open tool window needs a stable id to save the layout.");
                    containerNode.Windows.Add(new LayoutWindowEntry(id, window.State.ToString()));
                }

                return containerNode;

            case Workspace workspace:
                return new WorkspaceLayoutNode { RelativeSize = DockSite.GetRelativeSize(workspace) };

            default:
                throw new NotSupportedException(
                    $"The layout contains an element of type '{element.GetType().Name}'. Only SplitContainer, ToolWindowContainer and Workspace can be serialized.");
        }
    }

    private void Apply(DockSite site, LayoutDocument layout)
    {
        var workspaces = new Queue<Workspace>();
        CollectWorkspaces(site.Child, workspaces);

        var index = new Dictionary<string, ToolWindow>();
        foreach (var window in site.ToolWindows)
        {
            if (window.SerializationId is { } id)
            {
                index[id] = window;
            }
        }

        foreach (var window in site.ToolWindows)
        {
            window.IsRelocating = window.IsOpen;
        }

        // Hide the flyout, drop existing auto-hide groups and close the floating windows
        // before touching the tree, so every window they host is released and reusable.
        site.ClearAutoHideGroups();
        site.CloseFloatingWindows();

        // Detach the old tree and dismantle its split containers so every reusable element
        // (the workspace in particular) is fully released before the rebuild. Once the tree
        // is disconnected the visual parent links are no longer discoverable, so this must
        // happen eagerly rather than lazily during the rebuild.
        var oldRoot = site.Child;
        site.Child = null;
        DismantleSplits(oldRoot);

        var used = new HashSet<ToolWindow>();
        site.Child = layout.Root is null ? null : Build(layout.Root, index, workspaces, used);

        foreach (var groupNode in layout.AutoHideGroups)
        {
            var groupWindows = new List<ToolWindow>();
            foreach (var entry in groupNode.Windows)
            {
                if (Resolve(entry.Id, index) is { } window && !used.Contains(window))
                {
                    used.Add(window);
                    window.IsRelocating = true;
                    window.Container?.Items.Remove(window);
                    window.IsRelocating = false;
                    window.State = DockingWindowState.AutoHide;
                    window.IsOpen = true;
                    window.IsSelected = false;
                    groupWindows.Add(window);
                }
            }

            if (groupWindows.Count > 0)
            {
                var group = new AutoHideGroup(groupNode.Edge, groupWindows, groupNode.Size)
                {
                    Offset = groupNode.Offset,
                };

                if (ResolveSiblingReference(site, groupNode.RestoreSibling, index) is { } sibling)
                {
                    group.RestoreHint = new DockRestoreHint(
                        container: null,
                        sibling,
                        groupNode.RestoreSide,
                        groupNode.RestoreRelativeSize);
                }

                site.AddAutoHideGroup(group);
            }
        }

        foreach (var floatingNode in layout.FloatingWindows)
        {
            if (floatingNode.Root is null
                || Build(floatingNode.Root, index, workspaces, used) is not { } floatingContent)
            {
                continue;
            }

            // The hosted windows are marked as floating by the host itself, which also does it
            // for the ones docked inside it.
            var bounds = FloatingWindowHost.ClampToDisplay(
                new RectInt32(floatingNode.X, floatingNode.Y, floatingNode.Width, floatingNode.Height));
            var host = new FloatingWindowHost(site, floatingContent, bounds);

            var restoreContainer = ResolveSiblingReference(site, floatingNode.RestoreContainer, index) as ToolWindowContainer;
            var restoreSibling = ResolveSiblingReference(site, floatingNode.RestoreSibling, index);
            if (restoreContainer is not null || restoreSibling is not null)
            {
                host.RestoreHint = new DockRestoreHint(
                    restoreContainer,
                    restoreSibling,
                    floatingNode.RestoreSide,
                    floatingNode.RestoreRelativeSize);
            }

            site.AddFloatingHost(host);
        }

        foreach (var window in site.ToolWindows.ToList())
        {
            if (used.Contains(window))
            {
                window.IsRelocating = false;
                continue;
            }

            if (window.IsOpen)
            {
                window.IsRelocating = true;
                window.Container?.Items.Remove(window);
                window.IsRelocating = false;

                if (UnresolvedWindowBehavior == UnresolvedWindowBehavior.DockLeft)
                {
                    LayoutManager.DockToSide(site, window, DockSide.Left);
                }
                else
                {
                    window.IsOpen = false;
                    window.IsSelected = false;
                }
            }
            else
            {
                window.IsRelocating = false;
            }
        }

        site.NotifyLayoutChanged(LayoutChangeKind.LayoutLoaded);
    }

    private UIElement? Build(
        LayoutNode node,
        Dictionary<string, ToolWindow> index,
        Queue<Workspace> workspaces,
        HashSet<ToolWindow> used)
    {
        switch (node)
        {
            case SplitLayoutNode splitNode:
                var children = new List<UIElement>();
                foreach (var childNode in splitNode.Children)
                {
                    if (Build(childNode, index, workspaces, used) is { } child)
                    {
                        children.Add(child);
                    }
                }

                if (children.Count == 0)
                {
                    return null;
                }

                if (children.Count == 1)
                {
                    // A split with a single surviving pane collapses to that pane.
                    DockSite.SetRelativeSize(children[0], splitNode.RelativeSize);
                    return children[0];
                }

                var split = new SplitContainer { Orientation = splitNode.Orientation };
                DockSite.SetRelativeSize(split, splitNode.RelativeSize);
                foreach (var child in children)
                {
                    split.Children.Add(child);
                }

                return split;

            case ContainerLayoutNode containerNode:
                var container = new ToolWindowContainer();
                DockSite.SetRelativeSize(container, containerNode.RelativeSize);

                foreach (var entry in containerNode.Windows)
                {
                    if (Resolve(entry.Id, index) is { } window)
                    {
                        container.Items.Add(window);
                        used.Add(window);
                    }
                }

                if (container.Items.Count == 0)
                {
                    return null;
                }

                if (containerNode.SelectedId is { } selectedId
                    && container.Items.FirstOrDefault(w => w.SerializationId == selectedId) is { } selected)
                {
                    container.SelectedItem = selected;
                }

                return container;

            case WorkspaceLayoutNode workspaceNode:
                if (workspaces.Count == 0)
                {
                    return null;
                }

                var workspace = workspaces.Dequeue();
                DockSite.SetRelativeSize(workspace, workspaceNode.RelativeSize);
                return workspace;

            default:
                throw new NotSupportedException($"Unknown layout node type '{node.GetType().Name}'.");
        }
    }

    private ToolWindow? Resolve(string id, Dictionary<string, ToolWindow> index)
    {
        if (index.TryGetValue(id, out var window))
        {
            return window;
        }

        var args = new ToolWindowResolvingEventArgs(id);
        ToolWindowResolving?.Invoke(this, args);

        if (args.ToolWindow is { } resolved)
        {
            resolved.SerializationId ??= id;
            index[id] = resolved;
            return resolved;
        }

        return null;
    }

    /// <summary>
    /// Recursively empties the split containers of a discarded layout tree so reusable
    /// elements (workspaces) are no longer associated with their old parents.
    /// </summary>
    private static void DismantleSplits(UIElement? element)
    {
        if (element is not SplitContainer split)
        {
            return;
        }

        var panes = split.GetPanes();
        split.Children.Clear();
        foreach (var pane in panes)
        {
            DismantleSplits(pane);
        }
    }

    /// <summary>
    /// Converts a live restore-sibling element into a stable reference for the XML:
    /// "Workspace:n" for the n-th workspace in document order, "Window:id" for a tool window
    /// container. Split containers (and elements no longer in this site) yield no reference,
    /// so pinning back falls to the group's edge, same as when the sibling disappears at runtime.
    /// </summary>
    private static string? CaptureSiblingReference(DockSite site, FrameworkElement? sibling)
    {
        if (sibling is null || !ReferenceEquals(sibling.FindAncestor<DockSite>(), site))
        {
            return null;
        }

        switch (sibling)
        {
            case Workspace workspace:
                var workspaces = new Queue<Workspace>();
                CollectWorkspaces(site.Child, workspaces);
                var position = 0;
                foreach (var candidate in workspaces)
                {
                    if (ReferenceEquals(candidate, workspace))
                    {
                        return $"Workspace:{position}";
                    }

                    position++;
                }

                return null;

            case ToolWindowContainer container:
                var id = container.Items.FirstOrDefault(w => w.SerializationId is not null)?.SerializationId;
                return id is null ? null : $"Window:{id}";

            default:
                return null;
        }
    }

    /// <summary>Resolves a serialized restore-sibling reference against the rebuilt layout tree.</summary>
    private static FrameworkElement? ResolveSiblingReference(
        DockSite site,
        string? reference,
        Dictionary<string, ToolWindow> index)
    {
        const string workspacePrefix = "Workspace:";
        const string windowPrefix = "Window:";

        if (reference is null)
        {
            return null;
        }

        if (reference.StartsWith(workspacePrefix, StringComparison.Ordinal)
            && int.TryParse(reference[workspacePrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var position))
        {
            var workspaces = new Queue<Workspace>();
            CollectWorkspaces(site.Child, workspaces);
            return workspaces.Skip(position).FirstOrDefault();
        }

        if (reference.StartsWith(windowPrefix, StringComparison.Ordinal)
            && index.TryGetValue(reference[windowPrefix.Length..], out var window))
        {
            return window.Container;
        }

        return null;
    }

    private static void CollectWorkspaces(UIElement? element, Queue<Workspace> workspaces)
    {
        switch (element)
        {
            case Workspace workspace:
                workspaces.Enqueue(workspace);
                break;
            case SplitContainer split:
                foreach (var pane in split.GetPanes())
                {
                    CollectWorkspaces(pane, workspaces);
                }

                break;
        }
    }
}
