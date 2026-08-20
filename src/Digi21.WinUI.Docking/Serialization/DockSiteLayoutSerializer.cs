using System.Globalization;
using System.Text;
using Digi21.WinUI.Docking.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
    /// Raised while loading for each open window the layout does not mention and that is kept open
    /// anyway, just before it is put back into the layout, so the application can say where it
    /// goes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Raised whichever reason keeps the window open: <see cref="UnresolvedWindowBehavior"/> set to
    /// <see cref="Serialization.UnresolvedWindowBehavior.DockLeft"/>, or a window declared with
    /// <c>CanClose="False"</c>, which is kept whatever that setting says.
    /// </para>
    /// <para>
    /// Several windows can be kept open by one load, and they are placed one at a time, in the
    /// order the dock site registered them — tool windows first, then documents. A handler that
    /// docks a window against another one is therefore not looking at a settled layout: a sibling
    /// later in that order is still out of it, open but not yet placed. Test
    /// <see cref="DockingWindow.IsPlaced"/> rather than <see cref="DockingWindow.IsOpen"/>, which
    /// is true for every window being rescued, including those still waiting.
    /// </para>
    /// </remarks>
    public event EventHandler<UnresolvedWindowEventArgs>? UnresolvedWindowDocking;

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
                entries.Add(CaptureWindow(window));
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

    // Captures one window as a layout entry. Whether it may be auto-hidden travels with the layout
    // so that a panel meant to stay docked keeps that when the layout is reloaded, and so an
    // application can settle it from its initial layout file rather than from XAML.
    private static LayoutWindowEntry CaptureWindow(DockingWindow window)
    {
        return new LayoutWindowEntry(
            RequireId(window),
            window.State.ToString(),
            window is ToolWindow { CanAutoHide: false } ? false : null);
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
                    containerNode.Windows.Add(CaptureWindow(window));
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
                    groupNode.Windows.Add(new LayoutWindowEntry(
                        RequireId(document),
                        document.State.ToString(),
                        IsPinned: document is DocumentWindow { IsPinned: true },
                        IsProvisional: document is DocumentWindow { IsProvisional: true }));
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

    // Tells whether a window the loaded layout does not mention stays in the layout instead of
    // being closed.
    //
    // A window the user cannot close is not one a layout file gets to close either: there would be
    // no way to bring it back from the interface, and saving the layout on the way out would make
    // that permanent.
    internal static bool KeepsUnresolvedWindowOpen(UnresolvedWindowBehavior behavior, bool canClose)
    {
        return behavior == UnresolvedWindowBehavior.DockLeft || !canClose;
    }

    private void Apply(DockSite site, LayoutDocument layout)
    {
        var rebuild = new Rebuild(site);

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

        try
        {
            ApplyCore(site, layout, index, rebuild);
        }
        finally
        {
            // The load calls back into the application (the resolving events); one that throws
            // must not leave windows marked as relocating, since the mark suppresses their opened
            // and closed events and nothing else would ever clear it. The tree itself is not
            // rolled back — the application sees its own exception and a partial layout, but the
            // windows keep working.
            foreach (var window in KnownWindows(site))
            {
                window.IsRelocating = false;
            }
        }
    }

    private void ApplyCore(DockSite site, LayoutDocument layout, WindowIndex index, Rebuild rebuild)
    {
        // Hide the flyout, drop existing auto-hide groups and close the floating windows
        // before touching the tree, so every window they host is released and reusable.
        site.ClearAutoHideGroups();
        site.CloseFloatingWindows();

        // The tree is rebuilt in place, out of the elements it is already made of: the parts the
        // application declared (workspaces and document areas) are kept, and so are the split
        // containers and panes, which are interchangeable. Reloading a layout that has not changed
        // then moves nothing at all, which is what keeps content with a life cycle of its own from
        // being torn down for no reason.
        var used = new HashSet<DockingWindow>();
        var root = layout.Root is null ? null : Build(layout.Root, index, rebuild, used);

        root = rebuild.KeepUnmentionedElements(root);
        if (!ReferenceEquals(site.Child, root))
        {
            rebuild.Detach(root);
            site.Child = root;

            // Everything the new tree carries has just been moved, however little of it changed
            // place within the tree: this is where a workspace whose split became the root, with
            // nothing else about it altered, finds out that it was reparented.
            site.NotifyRelocated(root);
        }

        foreach (var groupNode in layout.AutoHideGroups)
        {
            var groupWindows = new List<ToolWindow>();
            var redockedWindows = new List<ToolWindow>();
            foreach (var entry in groupNode.Windows)
            {
                if (ResolveToolWindow(entry.Id, index) is { } window && !used.Contains(window))
                {
                    used.Add(window);
                    ApplyCanAutoHide(window, entry);
                    window.IsRelocating = true;
                    window.Container?.Items.Remove(window);
                    window.IsRelocating = false;

                    if (!window.CanAutoHide)
                    {
                        // The layout collapsed this window to an edge but the window no longer
                        // allows it — a file saved before the application settled on keeping the
                        // panel docked. Honoring the file would strand it: with auto-hide off its
                        // title bar has no pin button, so nothing in the interface could bring it
                        // back. It goes back into the layout instead, which the user can undo.
                        redockedWindows.Add(window);
                        continue;
                    }

                    window.State = DockingWindowState.AutoHide;
                    window.IsOpen = true;
                    window.IsSelected = false;
                    groupWindows.Add(window);
                }
            }

            // Where the group came from, resolved against the tree that has just been rebuilt.
            // Both the windows that stay collapsed and the ones that have to go back into the
            // layout are placed by it.
            DockRestoreHint? restoreHint = null;
            if (ResolveSiblingReference(site, groupNode.RestoreSibling, index) is { } sibling)
            {
                restoreHint = new DockRestoreHint(
                    container: null,
                    sibling,
                    groupNode.RestoreSide,
                    groupNode.RestoreRelativeSize);
            }

            if (redockedWindows.Count > 0)
            {
                // Down the same path as pinning the group from its edge, so a layout being
                // migrated puts the panel back beside the neighbour the file names, at the size it
                // records, instead of wherever a fresh dock would land it. When that neighbour is
                // gone the group's own edge takes over, which is still what the file asked for.
                LayoutManager.RestoreWindows(site, redockedWindows, restoreHint, groupNode.Edge);
            }

            if (groupWindows.Count > 0)
            {
                site.AddAutoHideGroup(new AutoHideGroup(groupNode.Edge, groupWindows, groupNode.Size)
                {
                    Offset = groupNode.Offset,
                    RestoreHint = restoreHint,
                });
            }
        }

        foreach (var floatingNode in layout.FloatingWindows)
        {
            if (floatingNode.Root is null
                || Build(floatingNode.Root, index, rebuild, used) is not { } floatingContent)
            {
                continue;
            }

            rebuild.Detach(floatingContent);

            // The hosted windows are marked as floating by the host itself, which also does it
            // for the ones docked inside it.
            var bounds = FloatingWindowHost.ClampToDisplay(
                new RectInt32(floatingNode.X, floatingNode.Y, floatingNode.Width, floatingNode.Height));
            var host = new FloatingWindowHost(site, floatingContent, bounds, activate: false);
            site.NotifyRelocated(floatingContent);

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

                if (KeepsUnresolvedWindowOpen(UnresolvedWindowBehavior, window.CanClose))
                {
                    KeepOpen(site, window);
                }
                else
                {
                    // Closed windows report Docked whatever they were, matching RemoveWindow: a
                    // stale Floating or AutoHide would make Activate and Float dead ends when the
                    // window is reopened later.
                    window.IsOpen = false;
                    window.IsSelected = false;
                    window.State = DockingWindowState.Docked;
                }
            }
            else
            {
                window.IsRelocating = false;
            }
        }

        site.NotifyLayoutChanged(LayoutChangeKind.LayoutLoaded);
    }

    // Enumerates every window the dock site knows about, open or closed.
    private static List<DockingWindow> KnownWindows(DockSite site)
    {
        var windows = new List<DockingWindow>(site.ToolWindows.Count + site.Documents.Count);
        windows.AddRange(site.ToolWindows);
        windows.AddRange(site.Documents);
        return windows;
    }

    // Applies what the layout says about auto-hiding, if it says anything. A file written before
    // the attribute existed leaves the window exactly as the application declared it, which is what
    // keeps those files loading unchanged.
    // Restores which document tab is pinned and which one is being previewed, once the documents are
    // in the group they belong to: both move a tab to one end of its group, so the flags have to be
    // set where they can be acted on. Applied in the order the file lists them, which is the order
    // each block keeps, so a layout whose blocks were mixed up by hand still settles into three.
    //
    // A file marking several provisional documents in one group — only a hand-edited one can — ends
    // up with the last of them provisional and the rest promoted, which is the group's own rule
    // applying rather than the file being trusted.
    private static void ApplyTabZone(List<LayoutWindowEntry> entries, List<DockingWindow> windows)
    {
        foreach (var entry in entries)
        {
            if (windows.FirstOrDefault(window => window.SerializationId == entry.Id) is DocumentWindow document)
            {
                document.IsPinned = entry.IsPinned;
                document.IsProvisional = entry.IsProvisional && !entry.IsPinned;
            }
        }
    }

    private static void ApplyCanAutoHide(DockingWindow window, LayoutWindowEntry entry)
    {
        if (entry.CanAutoHide is { } canAutoHide && window is ToolWindow tool)
        {
            tool.CanAutoHide = canAutoHide;
        }
    }

    // Puts a window the loaded layout does not mention back into the layout: documents reopen in
    // the document area, tool windows dock as a new pane at an edge.
    //
    // Which edge is not the layout file's business — the file never heard of this window — so it is
    // the application's: the window's own PreferredDockSide, which a handler of
    // UnresolvedWindowDocking can override or take over entirely.
    private void KeepOpen(DockSite site, DockingWindow window)
    {
        var args = new UnresolvedWindowEventArgs(window, PreferredSideOf(window));
        UnresolvedWindowDocking?.Invoke(this, args);

        if (args.Handled)
        {
            return;
        }

        if (window is DocumentWindow document && site.DocumentHost is { } host)
        {
            host.OpenDocument(document);
            return;
        }

        LayoutManager.DockToSide(site, window, args.Side);
    }

    // The edge a window says it belongs at. Only a tool window has one; a document is placed by its
    // document area and never by an edge.
    private static DockSide PreferredSideOf(DockingWindow window)
    {
        return window is ToolWindow tool ? tool.PreferredDockSide : DockSide.Left;
    }

    private UIElement? Build(
        LayoutNode node,
        WindowIndex index,
        Rebuild rebuild,
        HashSet<DockingWindow> used)
    {
        switch (node)
        {
            case SplitLayoutNode splitNode:
                var children = new List<UIElement>();
                foreach (var childNode in splitNode.Children)
                {
                    if (Build(childNode, index, rebuild, used) is { } child)
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

                var split = rebuild.TakeSplit(children);
                split.Orientation = splitNode.Orientation;
                DockSite.SetRelativeSize(split, splitNode.RelativeSize);
                rebuild.SetPanes(split, children);
                return split;

            case ContainerLayoutNode containerNode:
                return BuildContainer<ToolWindowContainer>(
                    containerNode.Windows, containerNode.SelectedId, containerNode.RelativeSize,
                    id => ResolveToolWindow(id, index), rebuild, used);

            case DocumentContainerLayoutNode groupNode:
                return BuildContainer<DocumentContainer>(
                    groupNode.Windows, groupNode.SelectedId, groupNode.RelativeSize,
                    id => ResolveDocument(id, index), rebuild, used);

            case DocumentHostLayoutNode hostNode:
                // Document areas are reused by position, like workspaces: they are declared by
                // the application and carry whatever it put around the documents.
                if (rebuild.TakeDocumentHost() is not { } host)
                {
                    return null;
                }

                DockSite.SetRelativeSize(host, hostNode.RelativeSize);
                var hostRoot = hostNode.Root is null ? null : Build(hostNode.Root, index, rebuild, used);
                rebuild.SetHostChild(host, hostRoot);
                return host;

            case WorkspaceLayoutNode workspaceNode:
                if (rebuild.TakeWorkspace() is not { } workspace)
                {
                    return null;
                }

                DockSite.SetRelativeSize(workspace, workspaceNode.RelativeSize);
                return workspace;

            default:
                throw new NotSupportedException($"Unknown layout node type '{node.GetType().Name}'.");
        }
    }

    private static UIElement? BuildContainer<T>(
        List<LayoutWindowEntry> entries,
        string? selectedId,
        double relativeSize,
        Func<string, DockingWindow?> resolve,
        Rebuild rebuild,
        HashSet<DockingWindow> used)
        where T : DockingWindowContainer, new()
    {
        var windows = new List<DockingWindow>(entries.Count);
        foreach (var entry in entries)
        {
            if (resolve(entry.Id) is { } window && used.Add(window))
            {
                ApplyCanAutoHide(window, entry);
                windows.Add(window);
            }
        }

        if (windows.Count == 0)
        {
            return null;
        }

        var container = rebuild.TakeContainer<T>(windows);
        DockSite.SetRelativeSize(container, relativeSize);
        rebuild.SetItems(container, windows);
        ApplyTabZone(entries, windows);

        if (selectedId is not null
            && container.Items.FirstOrDefault(w => w.SerializationId == selectedId) is { } selected)
        {
            container.SelectedItem = selected;
        }

        return container;
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

    // Converts a live restore-sibling element into a stable reference for the XML: "Workspace:n"
    // and "DocumentHost:n" for the n-th of those in document order, "Window:id" for a tool window
    // container and "Document:id" for a document group. Split containers (and elements no longer in
    // this site) yield no reference, so pinning back falls to the group's edge, same as when the
    // sibling disappears at runtime.
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

    // Resolves a serialized restore-sibling reference against the rebuilt layout tree.
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

    // Rebuilds a dock site's layout tree out of the elements it is already made of, instead of from
    // scratch.
    //
    // Split containers and panes are interchangeable, so they are handed out by position and by the
    // windows they hold rather than recreated; workspaces and document areas are declared by the
    // application and must be kept. An element is only ever taken out of its old parent when it is
    // going somewhere else, which is what makes reloading an unchanged layout move nothing at all.
    //
    // Where things currently are is tracked here rather than read back from the XAML tree: an
    // element that has just been moved has no visual parent until the next layout pass, and the
    // rebuild does all its work inside one.
    private sealed class Rebuild
    {
        private readonly DockSite site;
        private readonly Dictionary<UIElement, object> parents = [];
        private readonly Queue<Workspace> workspaces = [];
        private readonly Queue<DocumentHost> documentHosts = [];
        private readonly Queue<SplitContainer> splits = [];
        private readonly HashSet<DockingWindowContainer> usedContainers = [];

        internal Rebuild(DockSite site)
        {
            this.site = site;
            Collect(site, site.Child);

            // Floating windows hold layout trees of their own, and a load reuses their elements
            // exactly as it reuses the main tree's. Closing the hosts only unparents each tree's
            // root, so anything below it — the panes of a floating split — keeps its old parent;
            // without these entries Detach would not know how to release such an element, and
            // inserting it elsewhere would fail with the double-parent COMException (0x800F1000).
            foreach (var floatingHost in site.FloatingHosts)
            {
                Collect(floatingHost.Surface, floatingHost.LayoutChild);
            }
        }

        // Takes the next reusable workspace, or nothing when they are all spoken for.
        internal Workspace? TakeWorkspace() => workspaces.Count > 0 ? workspaces.Dequeue() : null;

        // Takes the next reusable document area, or nothing when they are all spoken for.
        internal DocumentHost? TakeDocumentHost() => documentHosts.Count > 0 ? documentHosts.Dequeue() : null;

        // Takes a split container to hold the given panes: one of the existing ones, unless it sits
        // inside a pane it would have to hold, which would make it its own ancestor.
        internal SplitContainer TakeSplit(List<UIElement> children)
        {
            while (splits.Count > 0)
            {
                var candidate = splits.Dequeue();
                if (!children.Any(child => IsUnder(candidate, child)))
                {
                    return candidate;
                }
            }

            return new SplitContainer();
        }

        // Takes the pane that holds the given windows, so windows that stay together keep the pane
        // they are in, or a new one when they come from panes already in use.
        internal DockingWindowContainer TakeContainer<T>(List<DockingWindow> windows)
            where T : DockingWindowContainer, new()
        {
            foreach (var window in windows)
            {
                if (window.Container is T candidate && usedContainers.Add(candidate))
                {
                    return candidate;
                }
            }

            var created = new T();
            usedContainers.Add(created);
            return created;
        }

        // Makes the tabs of a pane match the given windows, moving only what has to move.
        internal void SetItems(DockingWindowContainer container, List<DockingWindow> windows)
        {
            if (container.Items.SequenceEqual(windows))
            {
                return;
            }

            for (var i = container.Items.Count - 1; i >= 0; i--)
            {
                if (!windows.Contains(container.Items[i]))
                {
                    container.Items.RemoveAt(i);
                }
            }

            for (var i = 0; i < windows.Count; i++)
            {
                var at = container.Items.IndexOf(windows[i]);
                if (at == i)
                {
                    continue;
                }

                // Reordering tabs within a pane leaves the windows where they are in the XAML
                // tree; joining another pane is what moves them.
                var moved = !ReferenceEquals(windows[i].Container, container);

                if (at >= 0)
                {
                    container.Items.RemoveAt(at);
                }

                // Adding a window to a pane takes it out of the one it was in.
                container.Items.Insert(i, windows[i]);

                if (moved)
                {
                    site.NotifyRelocated(windows[i]);
                }
            }
        }

        // Makes the panes of a split container match the given children, in order.
        internal void SetPanes(SplitContainer split, List<UIElement> children)
        {
            var panes = split.GetPanes();
            if (panes.SequenceEqual(children))
            {
                foreach (var child in children)
                {
                    parents[child] = split;
                }

                return;
            }

            foreach (var pane in panes.Where(pane => !children.Contains(pane)))
            {
                split.Children.Remove(pane);
                parents.Remove(pane);
            }

            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                var at = split.GetPanes().IndexOf(child);
                if (at != i)
                {
                    if (at >= 0)
                    {
                        split.Children.Remove(child);
                    }
                    else
                    {
                        Detach(child);
                    }

                    split.Children.Insert(PanePosition(split, i), child);
                    site.NotifyRelocated(child);
                }

                parents[child] = split;
            }
        }

        // Makes a document area hold the given tree.
        internal void SetHostChild(DocumentHost host, UIElement? child)
        {
            if (!ReferenceEquals(host.Child, child))
            {
                Detach(child);
                host.Child = child;
                site.NotifyRelocated(child);
            }

            if (child is not null)
            {
                parents[child] = host;
            }
        }

        // Takes an element out of wherever it currently is.
        internal void Detach(UIElement? node)
        {
            if (node is null || !parents.Remove(node, out var parent))
            {
                return;
            }

            switch (parent)
            {
                case SplitContainer split:
                    split.Children.Remove(node);
                    break;

                case DocumentHost host when ReferenceEquals(host.Child, node):
                    host.Child = null;
                    break;

                case DockSite dockSite when ReferenceEquals(dockSite.Child, node):
                    dockSite.Child = null;
                    break;

                case FloatingWindowRoot floating when ReferenceEquals(floating.Child, node):
                    floating.Child = null;
                    break;
            }
        }

        // Puts back the workspaces and document areas the loaded layout says nothing about.
        //
        // They are declared by the application, not by the layout, and they carry its main content.
        // Dropping them would empty the window with no way of getting them back, and saving the
        // layout on the way out would make that permanent.
        internal UIElement? KeepUnmentionedElements(UIElement? root)
        {
            foreach (var element in Unmentioned())
            {
                Detach(element);
                DockSite.SetRelativeSize(element, 1.0);

                if (root is null)
                {
                    root = element;
                    continue;
                }

                Detach(root);
                DockSite.SetRelativeSize(root, 3.0);

                var split = new SplitContainer { Orientation = Orientation.Horizontal };
                split.Children.Add(root);
                split.Children.Add(element);
                site.NotifyRelocated(split);
                root = split;
            }

            return root;
        }

        private IEnumerable<FrameworkElement> Unmentioned()
        {
            while (workspaces.Count > 0)
            {
                yield return workspaces.Dequeue();
            }

            while (documentHosts.Count > 0)
            {
                yield return documentHosts.Dequeue();
            }
        }

        // Maps a pane position to a position in a split container's children, which also hold its
        // splitters.
        private static int PanePosition(SplitContainer split, int paneIndex)
        {
            var panes = 0;
            for (var i = 0; i < split.Children.Count; i++)
            {
                if (split.Children[i] is DockSplitter)
                {
                    return i;
                }

                if (panes == paneIndex)
                {
                    return i;
                }

                panes++;
            }

            return split.Children.Count;
        }

        private bool IsUnder(UIElement node, UIElement ancestor)
        {
            var current = (object)node;
            while (current is UIElement element && parents.TryGetValue(element, out var parent))
            {
                if (ReferenceEquals(parent, ancestor))
                {
                    return true;
                }

                current = parent;
            }

            return false;
        }

        private void Collect(object parent, UIElement? element)
        {
            if (element is null)
            {
                return;
            }

            parents[element] = parent;

            switch (element)
            {
                case Workspace workspace:
                    workspaces.Enqueue(workspace);
                    break;

                case DocumentHost host:
                    documentHosts.Enqueue(host);
                    Collect(host, host.Child);
                    break;

                // Enqueued after its panes: the rebuild works bottom up, asking for the split of
                // the innermost node first, and the two orders have to agree for an unchanged
                // layout to get its own split containers back.
                case SplitContainer split:
                    foreach (var pane in split.GetPanes())
                    {
                        Collect(split, pane);
                    }

                    splits.Enqueue(split);
                    break;
            }
        }
    }

    // The tool windows and documents a load can match serialized ids against.
    private sealed class WindowIndex
    {
        internal Dictionary<string, ToolWindow> ToolWindows { get; } = [];

        internal Dictionary<string, DocumentWindow> Documents { get; } = [];
    }

    // The elements of a layout that are declared by the application and reused across a load
    // instead of being rebuilt: they are matched by their position in document order.
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
