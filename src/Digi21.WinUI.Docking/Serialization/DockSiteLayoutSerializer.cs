using System.Text;
using Microsoft.UI.Xaml;

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

        var root = dockSite.Child is null ? null : Capture(dockSite.Child);
        LayoutXml.Write(stream, root);
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

        var rootNode = LayoutXml.Read(stream);
        Apply(dockSite, rootNode);
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

    private void Apply(DockSite site, LayoutNode? rootNode)
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

        // Detach the old tree and dismantle its split containers so every reusable element
        // (the workspace in particular) is fully released before the rebuild. Once the tree
        // is disconnected the visual parent links are no longer discoverable, so this must
        // happen eagerly rather than lazily during the rebuild.
        var oldRoot = site.Child;
        site.Child = null;
        DismantleSplits(oldRoot);

        var used = new HashSet<ToolWindow>();
        site.Child = rootNode is null ? null : Build(rootNode, index, workspaces, used);

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
