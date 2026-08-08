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
    internal const int CurrentVersion = 1;

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
            foreach (var window in group.Windows)
            {
                writer.WriteStartElement("ToolWindow");
                writer.WriteAttributeString("Id", window.Id);
                writer.WriteAttributeString("State", window.State);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
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

                foreach (var child in element.Elements("ToolWindow"))
                {
                    var id = (string?)child.Attribute("Id")
                        ?? throw new InvalidDataException("A ToolWindow element is missing its Id attribute.");
                    group.Windows.Add(new LayoutWindowEntry(id, (string?)child.Attribute("State") ?? nameof(DockingWindowState.AutoHide)));
                }

                layout.AutoHideGroups.Add(group);
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
