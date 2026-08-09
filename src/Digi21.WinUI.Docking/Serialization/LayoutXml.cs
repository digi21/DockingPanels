using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.UI.Xaml.Controls;

namespace Digi21.WinUI.Docking.Serialization;

/// <summary>
/// Reads and writes the versioned XML layout format. Implemented with explicit
/// XmlWriter/XLinq code (no reflection-based serialization) so it is trimming-friendly
/// and the schema cannot drift by accident.
/// </summary>
internal static class LayoutXml
{
    internal const string RootElementName = "DockSiteLayout";

    /// <summary>
    /// The format version written by this library. Version 3 added the document area
    /// (<c>DocumentHost</c> and <c>DocumentContainer</c> nodes); version 2 replaced the flat
    /// window list of a floating window with a layout tree, so windows docked inside it are
    /// preserved; version 1 documents are still read, their window list becoming a single
    /// container. Older versions are read unchanged, they simply have no document area.
    /// </summary>
    internal const int CurrentVersion = 3;

    internal static void Write(Stream stream, LayoutDocument layout)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };

        using var writer = XmlWriter.Create(stream, settings);
        writer.WriteStartElement(RootElementName);
        writer.WriteAttributeString("Version", CurrentVersion.ToString(CultureInfo.InvariantCulture));

        if (layout.Root is not null)
        {
            WriteNode(writer, layout.Root);
        }

        foreach (var group in layout.AutoHideGroups)
        {
            writer.WriteStartElement("AutoHideGroup");
            writer.WriteAttributeString("Edge", group.Edge.ToString());
            writer.WriteAttributeString("Size", group.Size.ToString("R", CultureInfo.InvariantCulture));
            if (group.Offset > 0)
            {
                writer.WriteAttributeString("Offset", group.Offset.ToString("R", CultureInfo.InvariantCulture));
            }

            if (group.RestoreSibling is not null)
            {
                writer.WriteAttributeString("RestoreSibling", group.RestoreSibling);
                writer.WriteAttributeString("RestoreSide", group.RestoreSide.ToString());
                writer.WriteAttributeString("RestoreRelativeSize", group.RestoreRelativeSize.ToString("R", CultureInfo.InvariantCulture));
            }

            WriteWindows(writer, group.Windows);
            writer.WriteEndElement();
        }

        foreach (var floating in layout.FloatingWindows)
        {
            writer.WriteStartElement("FloatingWindow");
            writer.WriteAttributeString("X", floating.X.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("Y", floating.Y.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("Width", floating.Width.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("Height", floating.Height.ToString(CultureInfo.InvariantCulture));

            if (floating.RestoreContainer is not null)
            {
                writer.WriteAttributeString("RestoreContainer", floating.RestoreContainer);
            }

            if (floating.RestoreSibling is not null)
            {
                writer.WriteAttributeString("RestoreSibling", floating.RestoreSibling);
                writer.WriteAttributeString("RestoreSide", floating.RestoreSide.ToString());
                writer.WriteAttributeString("RestoreRelativeSize", floating.RestoreRelativeSize.ToString("R", CultureInfo.InvariantCulture));
            }

            if (floating.Root is not null)
            {
                WriteNode(writer, floating.Root);
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteWindows(XmlWriter writer, List<LayoutWindowEntry> windows)
    {
        foreach (var window in windows)
        {
            writer.WriteStartElement("ToolWindow");
            writer.WriteAttributeString("Id", window.Id);
            writer.WriteAttributeString("State", window.State);
            writer.WriteEndElement();
        }
    }

    internal static LayoutDocument Read(Stream stream)
    {
        var document = XDocument.Load(stream);
        var root = document.Root ?? throw new InvalidDataException("The layout document has no root element.");

        if (root.Name.LocalName != RootElementName)
        {
            throw new InvalidDataException($"Expected a '{RootElementName}' root element but found '{root.Name.LocalName}'.");
        }

        var version = (int?)root.Attribute("Version") ?? CurrentVersion;
        if (version > CurrentVersion)
        {
            throw new NotSupportedException($"The layout was saved with a newer format version ({version}) than this library supports ({CurrentVersion}).");
        }

        var layout = new LayoutDocument();

        foreach (var element in root.Elements())
        {
            if (element.Name.LocalName == "AutoHideGroup")
            {
                var group = new AutoHideGroupNode
                {
                    Edge = Enum.TryParse<DockSide>((string?)element.Attribute("Edge"), out var edge) ? edge : DockSide.Left,
                };

                var sizeText = (string?)element.Attribute("Size");
                if (double.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var size) && size > 0)
                {
                    group.Size = size;
                }

                var offsetText = (string?)element.Attribute("Offset");
                if (double.TryParse(offsetText, NumberStyles.Float, CultureInfo.InvariantCulture, out var offset) && offset > 0)
                {
                    group.Offset = offset;
                }

                group.RestoreSibling = (string?)element.Attribute("RestoreSibling");
                if (Enum.TryParse<DockSide>((string?)element.Attribute("RestoreSide"), out var restoreSide))
                {
                    group.RestoreSide = restoreSide;
                }

                var restoreRelativeText = (string?)element.Attribute("RestoreRelativeSize");
                if (double.TryParse(restoreRelativeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var restoreRelative) && restoreRelative > 0)
                {
                    group.RestoreRelativeSize = restoreRelative;
                }

                ReadWindows(element, group.Windows, nameof(DockingWindowState.AutoHide));
                layout.AutoHideGroups.Add(group);
            }
            else if (element.Name.LocalName == "FloatingWindow")
            {
                var floating = new FloatingWindowNode
                {
                    X = ReadInt(element, "X", 0),
                    Y = ReadInt(element, "Y", 0),
                    Width = ReadInt(element, "Width", 380),
                    Height = ReadInt(element, "Height", 300),
                    RestoreContainer = (string?)element.Attribute("RestoreContainer"),
                    RestoreSibling = (string?)element.Attribute("RestoreSibling"),
                };

                if (Enum.TryParse<DockSide>((string?)element.Attribute("RestoreSide"), out var floatingSide))
                {
                    floating.RestoreSide = floatingSide;
                }

                var floatingRelativeText = (string?)element.Attribute("RestoreRelativeSize");
                if (double.TryParse(floatingRelativeText, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatingRelative)
                    && floatingRelative > 0)
                {
                    floating.RestoreRelativeSize = floatingRelative;
                }

                floating.Root = ReadFloatingRoot(element);
                layout.FloatingWindows.Add(floating);
            }
            else if (layout.Root is null)
            {
                layout.Root = ReadNode(element);
            }
            else
            {
                throw new InvalidDataException("The layout document has more than one root layout node.");
            }
        }

        return layout;
    }

    /// <summary>
    /// Reads the layout tree of a floating window. Version 1 wrote the hosted windows as a flat
    /// list, which is the same thing a floating window with a single pane holds, so it is read
    /// back as one container.
    /// </summary>
    private static LayoutNode? ReadFloatingRoot(XElement element)
    {
        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName is "SplitContainer" or "ToolWindowContainer" or "Workspace"
                or "DocumentContainer" or "DocumentHost")
            {
                return ReadNode(child);
            }
        }

        var container = new ContainerLayoutNode { SelectedId = (string?)element.Attribute("SelectedId") };
        ReadWindows(element, container.Windows, nameof(DockingWindowState.Floating));
        return container.Windows.Count > 0 ? container : null;
    }

    private static void ReadWindows(XElement element, List<LayoutWindowEntry> windows, string defaultState)
    {
        foreach (var child in element.Elements("ToolWindow"))
        {
            var id = (string?)child.Attribute("Id")
                ?? throw new InvalidDataException("A ToolWindow element is missing its Id attribute.");
            windows.Add(new LayoutWindowEntry(id, (string?)child.Attribute("State") ?? defaultState));
        }
    }

    private static int ReadInt(XElement element, string name, int fallback)
    {
        var text = (string?)element.Attribute(name);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private static void WriteNode(XmlWriter writer, LayoutNode node)
    {
        switch (node)
        {
            case SplitLayoutNode split:
                writer.WriteStartElement("SplitContainer");
                writer.WriteAttributeString("Orientation", split.Orientation.ToString());
                WriteRelativeSize(writer, split);
                foreach (var child in split.Children)
                {
                    WriteNode(writer, child);
                }

                writer.WriteEndElement();
                break;

            case ContainerLayoutNode container:
                writer.WriteStartElement("ToolWindowContainer");
                WriteRelativeSize(writer, container);
                if (container.SelectedId is not null)
                {
                    writer.WriteAttributeString("SelectedId", container.SelectedId);
                }

                foreach (var window in container.Windows)
                {
                    writer.WriteStartElement("ToolWindow");
                    writer.WriteAttributeString("Id", window.Id);
                    writer.WriteAttributeString("State", window.State);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                break;

            case WorkspaceLayoutNode workspace:
                writer.WriteStartElement("Workspace");
                WriteRelativeSize(writer, workspace);
                writer.WriteEndElement();
                break;

            case DocumentHostLayoutNode host:
                writer.WriteStartElement("DocumentHost");
                WriteRelativeSize(writer, host);
                if (host.Root is not null)
                {
                    WriteNode(writer, host.Root);
                }

                writer.WriteEndElement();
                break;

            case DocumentContainerLayoutNode group:
                writer.WriteStartElement("DocumentContainer");
                WriteRelativeSize(writer, group);
                if (group.SelectedId is not null)
                {
                    writer.WriteAttributeString("SelectedId", group.SelectedId);
                }

                foreach (var document in group.Windows)
                {
                    writer.WriteStartElement("Document");
                    writer.WriteAttributeString("Id", document.Id);
                    writer.WriteAttributeString("State", document.State);
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                break;

            default:
                throw new NotSupportedException($"Unknown layout node type '{node.GetType().Name}'.");
        }
    }

    private static LayoutNode ReadNode(XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "SplitContainer":
                var split = new SplitLayoutNode
                {
                    Orientation = Enum.TryParse<Orientation>((string?)element.Attribute("Orientation"), out var orientation)
                        ? orientation
                        : Orientation.Horizontal,
                    RelativeSize = ReadRelativeSize(element),
                };
                foreach (var child in element.Elements())
                {
                    split.Children.Add(ReadNode(child));
                }

                return split;

            case "ToolWindowContainer":
                var container = new ContainerLayoutNode
                {
                    RelativeSize = ReadRelativeSize(element),
                    SelectedId = (string?)element.Attribute("SelectedId"),
                };
                foreach (var child in element.Elements("ToolWindow"))
                {
                    var id = (string?)child.Attribute("Id")
                        ?? throw new InvalidDataException("A ToolWindow element is missing its Id attribute.");
                    container.Windows.Add(new LayoutWindowEntry(id, (string?)child.Attribute("State") ?? nameof(DockingWindowState.Docked)));
                }

                return container;

            case "Workspace":
                return new WorkspaceLayoutNode { RelativeSize = ReadRelativeSize(element) };

            case "DocumentHost":
                var host = new DocumentHostLayoutNode { RelativeSize = ReadRelativeSize(element) };
                foreach (var child in element.Elements())
                {
                    host.Root = ReadNode(child);
                    break;
                }

                return host;

            case "DocumentContainer":
                var group = new DocumentContainerLayoutNode
                {
                    RelativeSize = ReadRelativeSize(element),
                    SelectedId = (string?)element.Attribute("SelectedId"),
                };
                foreach (var child in element.Elements("Document"))
                {
                    var documentId = (string?)child.Attribute("Id")
                        ?? throw new InvalidDataException("A Document element is missing its Id attribute.");
                    group.Windows.Add(new LayoutWindowEntry(
                        documentId,
                        (string?)child.Attribute("State") ?? nameof(DockingWindowState.Docked)));
                }

                return group;

            default:
                throw new InvalidDataException($"Unknown layout element '{element.Name.LocalName}'.");
        }
    }

    private static void WriteRelativeSize(XmlWriter writer, LayoutNode node)
    {
        writer.WriteAttributeString("RelativeSize", node.RelativeSize.ToString("R", CultureInfo.InvariantCulture));
    }

    private static double ReadRelativeSize(XElement element)
    {
        var text = (string?)element.Attribute("RelativeSize");
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : 1.0;
    }
}
