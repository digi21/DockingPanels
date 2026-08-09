using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace Digi21.WinUI.Docking.Serialization;

/// <summary>
/// Saves and restores the docking layout of a <see cref="DockSite"/> as versioned XML.
/// Only the structure is persisted (containers, splits, proportions, tab order and selection);
/// window instances and their content are matched by <see cref="DockingWindow.SerializationId"/>
/// and reused, so control state survives a layout reload.
/// </summary>
public class DockSiteLayoutSerializer
{
    /// <summary>
    /// Raised while loading when a serialized window id does not match any registered tool
    /// window, giving the application a chance to create it on demand.
    /// </summary>
    public event EventHandler<ToolWindowResolvingEventArgs>? ToolWindowResolving;

    /// <summary>
    /// Raised while loading when a serialized document id does not match any registered
    /// document, giving the application a chance to reopen it on demand.
    /// </summary>
    public event EventHandler<DocumentResolvingEventArgs>? DocumentResolving;

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

        static void CaptureWindows(IEnumerable<DockingWindow> windows, List<LayoutWindowEntry> entries)
        {
            foreach (var window in windows)
            {
                entries.Add(new LayoutWindowEntry(RequireId(window), window.State.ToString()));
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

    private static string RequireId(DockingWindow window)
    {
        return window.SerializationId
            ?? throw new InvalidOperationException(
                $"The window '{window.Title}' has no SerializationId. Every open window needs a stable id to save the layout.");
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
                    containerNode.Windows.Add(new LayoutWindowEntry(RequireId(window), window.State.ToString()));
                }

                return containerNode;

            case DocumentContainer group:
                var groupNode = new DocumentContainerLayoutNode
                {
                    RelativeSize = DockSite.GetRelativeSize(group),
                    SelectedId = group.SelectedItem?.SerializationId,
                };
                foreach (var document in group.Items)
                {
                    groupNode.Windows.Add(new LayoutWindowEntry(RequireId(document), document.State.ToString()));
                }

                return groupNode;

            case DocumentHost host:
                return new DocumentHostLayoutNode
                {
                    RelativeSize = DockSite.GetRelativeSize(host),
                    Root = host.Child is null ? null : Capture(host.Child),
                };

            case Workspace workspace:
                return new WorkspaceLayoutNode { RelativeSize = DockSite.GetRelativeSize(workspace) };

            default:
                throw new NotSupportedException(
                    $"The layout contains an element of type '{element.GetType().Name}'. Only SplitContainer, ToolWindowContainer, DocumentHost, DocumentContainer and Workspace can be serialized.");
        }
    }

    private void Apply(DockSite site, LayoutDocument layout)
    {
        var reusable = new ReusableElements(site.Child);

        var index = new WindowIndex();
        foreach (var window in site.ToolWindows)
        {
            if (window.SerializationId is { } id)
            {
                index.ToolWindows[id] = window;
            }
        }

        foreach (var document in site.Documents)
        {
            if (document.SerializationId is { } id)
            {
                index.Documents[id] = document;
            }
        }

        foreach (var window in KnownWindows(site))
        {
            window.IsRelocating = window.IsOpen;
        }

        // Hide the flyout, drop existing auto-hide groups and close the floating windows
        // before touching the tree, so every window they host is released and reusable.
        site.ClearAutoHideGroups();
        site.CloseFloatingWindows();

        // Detach the old tree and dismantle its split containers and document areas so every
        // reusable element (the workspace and the document hosts in particular) is fully
        // released before the rebuild. Once the tree is disconnected the visual parent links
        // are no longer discoverable, so this must happen eagerly rather than lazily during
        // the rebuild.
        var oldRoot = site.Child;
        site.Child = null;
        Dismantle(oldRoot);

        var used = new HashSet<DockingWindow>();
        site.Child = layout.Root is null ? null : Build(layout.Root, index, reusable, used);

        foreach (var groupNode in layout.AutoHideGroups)
        {
            var groupWindows = new List<ToolWindow>();
            foreach (var entry in groupNode.Windows)
            {
                if (ResolveToolWindow(entry.Id, index) is { } window && !used.Contains(window))
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
                || Build(floatingNode.Root, index, reusable, used) is not { } floatingContent)
            {
                continue;
            }

            // The hosted windows are marked as floating by the host itself, which also does it
            // for the ones docked inside it.
            var bounds = FloatingWindowHost.ClampToDisplay(
                new RectInt32(floatingNode.X, floatingNode.Y, floatingNode.Width, floatingNode.Height));
            var host = new FloatingWindowHost(site, floatingContent, bounds);

            var restoreContainer = ResolveSiblingReference(site, floatingNode.RestoreContainer, index) as DockingWindowContainer;
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

        foreach (var window in KnownWindows(site))
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
                    KeepOpen(site, window);
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

    /// <summary>Enumerates every window the dock site knows about, open or closed.</summary>
    private static List<DockingWindow> KnownWindows(DockSite site)
    {
        var windows = new List<DockingWindow>(site.ToolWindows.Count + site.Documents.Count);
        windows.AddRange(site.ToolWindows);
        windows.AddRange(site.Documents);
        return windows;
    }

    /// <summary>
    /// Puts a window the loaded layout does not mention back into the layout, for
    /// <see cref="UnresolvedWindowBehavior.DockLeft"/>: documents reopen in the document area,
    /// tool windows dock to the left edge.
    /// </summary>
    private static void KeepOpen(DockSite site, DockingWindow window)
    {
        if (window is DocumentWindow document && site.DocumentHost is { } host)
        {
            host.OpenDocument(document);
            return;
        }

        LayoutManager.DockToSide(site, window, DockSide.Left);
    }

    private UIElement? Build(
        LayoutNode node,
        WindowIndex index,
        ReusableElements reusable,
        HashSet<DockingWindow> used)
    {
        switch (node)
        {
            case SplitLayoutNode splitNode:
                var children = new List<UIElement>();
                foreach (var childNode in splitNode.Children)
                {
                    if (Build(childNode, index, reusable, used) is { } child)
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
                FillContainer(container, containerNode.Windows, containerNode.SelectedId, id => ResolveToolWindow(id, index), used);
                return container.Items.Count == 0 ? null : container;

            case DocumentContainerLayoutNode groupNode:
                var group = new DocumentContainer();
                DockSite.SetRelativeSize(group, groupNode.RelativeSize);
                FillContainer(group, groupNode.Windows, groupNode.SelectedId, id => ResolveDocument(id, index), used);
                return group.Items.Count == 0 ? null : group;

            case DocumentHostLayoutNode hostNode:
                if (reusable.DocumentHosts.Count == 0)
                {
                    return null;
                }

                // Document areas are reused by position, like workspaces: they are declared by
                // the application and carry whatever it put around the documents.
                var host = reusable.DocumentHosts.Dequeue();
                DockSite.SetRelativeSize(host, hostNode.RelativeSize);
                host.Child = hostNode.Root is null ? null : Build(hostNode.Root, index, reusable, used);
                return host;

            case WorkspaceLayoutNode workspaceNode:
                if (reusable.Workspaces.Count == 0)
                {
                    return null;
                }

                var workspace = reusable.Workspaces.Dequeue();
                DockSite.SetRelativeSize(workspace, workspaceNode.RelativeSize);
                return workspace;

            default:
                throw new NotSupportedException($"Unknown layout node type '{node.GetType().Name}'.");
        }
    }

    private static void FillContainer(
        DockingWindowContainer container,
        List<LayoutWindowEntry> entries,
        string? selectedId,
        Func<string, DockingWindow?> resolve,
        HashSet<DockingWindow> used)
    {
        foreach (var entry in entries)
        {
            if (resolve(entry.Id) is { } window)
            {
                container.Items.Add(window);
                used.Add(window);
            }
        }

        if (selectedId is not null
            && container.Items.FirstOrDefault(w => w.SerializationId == selectedId) is { } selected)
        {
            container.SelectedItem = selected;
        }
    }

    private ToolWindow? ResolveToolWindow(string id, WindowIndex index)
    {
        if (index.ToolWindows.TryGetValue(id, out var window))
        {
            return window;
        }

        var args = new ToolWindowResolvingEventArgs(id);
        ToolWindowResolving?.Invoke(this, args);

        if (args.ToolWindow is { } resolved)
        {
            resolved.SerializationId ??= id;
            index.ToolWindows[id] = resolved;
            return resolved;
        }

        return null;
    }

    private DocumentWindow? ResolveDocument(string id, WindowIndex index)
    {
        if (index.Documents.TryGetValue(id, out var document))
        {
            return document;
        }

        var args = new DocumentResolvingEventArgs(id);
        DocumentResolving?.Invoke(this, args);

        if (args.Document is { } resolved)
        {
            resolved.SerializationId ??= id;
            index.Documents[id] = resolved;
            return resolved;
        }

        return null;
    }

    /// <summary>
    /// Recursively empties the containers of a discarded layout tree so reusable elements
    /// (workspaces and document areas) are no longer associated with their old parents.
    /// </summary>
    private static void Dismantle(UIElement? element)
    {
        switch (element)
        {
            case SplitContainer split:
                var panes = split.GetPanes();
                split.Children.Clear();
                foreach (var pane in panes)
                {
                    Dismantle(pane);
                }

                break;

            case DocumentHost host:
                var child = host.Child;
                host.Child = null;
                Dismantle(child);
                break;
        }
    }

    /// <summary>
    /// Converts a live restore-sibling element into a stable reference for the XML:
    /// "Workspace:n" and "DocumentHost:n" for the n-th of those in document order, "Window:id"
    /// for a tool window container and "Document:id" for a document group. Split containers (and
    /// elements no longer in this site) yield no reference, so pinning back falls to the group's
    /// edge, same as when the sibling disappears at runtime.
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
                var workspacePosition = PositionOf(new ReusableElements(site.Child).Workspaces, workspace);
                return workspacePosition < 0 ? null : $"Workspace:{workspacePosition}";

            case DocumentHost host:
                var hostPosition = PositionOf(new ReusableElements(site.Child).DocumentHosts, host);
                return hostPosition < 0 ? null : $"DocumentHost:{hostPosition}";

            case DocumentContainer group:
                var documentId = group.Items.FirstOrDefault(w => w.SerializationId is not null)?.SerializationId;
                return documentId is null ? null : $"Document:{documentId}";

            case DockingWindowContainer container:
                var id = container.Items.FirstOrDefault(w => w.SerializationId is not null)?.SerializationId;
                return id is null ? null : $"Window:{id}";

            default:
                return null;
        }
    }

    private static int PositionOf<T>(Queue<T> candidates, T element)
        where T : class
    {
        var position = 0;
        foreach (var candidate in candidates)
        {
            if (ReferenceEquals(candidate, element))
            {
                return position;
            }

            position++;
        }

        return -1;
    }

    /// <summary>Resolves a serialized restore-sibling reference against the rebuilt layout tree.</summary>
    private static FrameworkElement? ResolveSiblingReference(DockSite site, string? reference, WindowIndex index)
    {
        if (reference is null)
        {
            return null;
        }

        var separator = reference.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0)
        {
            return null;
        }

        var kind = reference[..separator];
        var value = reference[(separator + 1)..];

        switch (kind)
        {
            case "Workspace":
                return At(new ReusableElements(site.Child).Workspaces, value);

            case "DocumentHost":
                return At(new ReusableElements(site.Child).DocumentHosts, value);

            case "Window":
                return index.ToolWindows.TryGetValue(value, out var window) ? window.Container : null;

            case "Document":
                return index.Documents.TryGetValue(value, out var document) ? document.Container : null;

            default:
                return null;
        }

        static FrameworkElement? At<T>(Queue<T> candidates, string position)
            where T : FrameworkElement
        {
            return int.TryParse(position, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? candidates.Skip(value).FirstOrDefault()
                : null;
        }
    }

    /// <summary>The tool windows and documents a load can match serialized ids against.</summary>
    private sealed class WindowIndex
    {
        internal Dictionary<string, ToolWindow> ToolWindows { get; } = [];

        internal Dictionary<string, DocumentWindow> Documents { get; } = [];
    }

    /// <summary>
    /// The elements of a layout that are declared by the application and reused across a load
    /// instead of being rebuilt: they are matched by their position in document order.
    /// </summary>
    private sealed class ReusableElements
    {
        internal ReusableElements(UIElement? root)
        {
            Collect(root);
        }

        internal Queue<Workspace> Workspaces { get; } = [];

        internal Queue<DocumentHost> DocumentHosts { get; } = [];

        private void Collect(UIElement? element)
        {
            switch (element)
            {
                case Workspace workspace:
                    Workspaces.Enqueue(workspace);
                    break;

                case DocumentHost host:
                    DocumentHosts.Enqueue(host);
                    break;

                case SplitContainer split:
                    foreach (var pane in split.GetPanes())
                    {
                        Collect(pane);
                    }

                    break;
            }
        }
    }
}
