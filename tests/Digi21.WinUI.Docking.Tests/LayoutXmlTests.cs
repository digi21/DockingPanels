using System.Text;
using Digi21.WinUI.Docking.Serialization;
using Microsoft.UI.Xaml.Controls;
using Xunit;

namespace Digi21.WinUI.Docking.Tests;

public class LayoutXmlTests
{
    [Fact]
    public void RoundTrip_PreservesStructure()
    {
        var original = new SplitLayoutNode
        {
            Orientation = Orientation.Horizontal,
            RelativeSize = 1.0,
        };
        var left = new ContainerLayoutNode { RelativeSize = 0.25, SelectedId = "a" };
        left.Windows.Add(new LayoutWindowEntry("a", "Docked"));
        left.Windows.Add(new LayoutWindowEntry("b", "Docked"));
        var inner = new SplitLayoutNode { Orientation = Orientation.Vertical, RelativeSize = 0.75 };
        inner.Children.Add(new WorkspaceLayoutNode { RelativeSize = 0.7 });
        var bottom = new ContainerLayoutNode { RelativeSize = 0.3 };
        bottom.Windows.Add(new LayoutWindowEntry("c", "Docked"));
        inner.Children.Add(bottom);
        original.Children.Add(left);
        original.Children.Add(inner);

        var restored = RoundTrip(original);

        var restoredSplit = Assert.IsType<SplitLayoutNode>(restored);
        Assert.Equal(Orientation.Horizontal, restoredSplit.Orientation);
        Assert.Equal(2, restoredSplit.Children.Count);

        var restoredLeft = Assert.IsType<ContainerLayoutNode>(restoredSplit.Children[0]);
        Assert.Equal(0.25, restoredLeft.RelativeSize);
        Assert.Equal("a", restoredLeft.SelectedId);
        Assert.Equal(["a", "b"], restoredLeft.Windows.Select(w => w.Id));

        var restoredInner = Assert.IsType<SplitLayoutNode>(restoredSplit.Children[1]);
        Assert.Equal(Orientation.Vertical, restoredInner.Orientation);
        Assert.Equal(0.75, restoredInner.RelativeSize);

        var restoredWorkspace = Assert.IsType<WorkspaceLayoutNode>(restoredInner.Children[0]);
        Assert.Equal(0.7, restoredWorkspace.RelativeSize);

        var restoredBottom = Assert.IsType<ContainerLayoutNode>(restoredInner.Children[1]);
        Assert.Null(restoredBottom.SelectedId);
        Assert.Equal(["c"], restoredBottom.Windows.Select(w => w.Id));
    }

    [Fact]
    public void RoundTrip_EmptyLayout_ReturnsNull()
    {
        Assert.Null(RoundTrip(null));
    }

    [Fact]
    public void Read_NewerVersion_Throws()
    {
        var xml = """<DockSiteLayout Version="999" />""";
        Assert.Throws<NotSupportedException>(() => Read(xml));
    }

    [Fact]
    public void Read_MissingVersion_IsAccepted()
    {
        Assert.Null(Read("""<DockSiteLayout />"""));
    }

    [Fact]
    public void Read_UnknownElement_Throws()
    {
        var xml = """<DockSiteLayout Version="1"><FloatingWindow /></DockSiteLayout>""";
        Assert.Throws<InvalidDataException>(() => Read(xml));
    }

    [Fact]
    public void Read_WrongRootElement_Throws()
    {
        Assert.Throws<InvalidDataException>(() => Read("<SomethingElse />"));
    }

    [Fact]
    public void Read_ToolWindowWithoutId_Throws()
    {
        var xml = """
            <DockSiteLayout Version="1">
              <ToolWindowContainer><ToolWindow /></ToolWindowContainer>
            </DockSiteLayout>
            """;
        Assert.Throws<InvalidDataException>(() => Read(xml));
    }

    [Fact]
    public void Read_InvalidRelativeSize_FallsBackToOne()
    {
        var xml = """
            <DockSiteLayout Version="1">
              <Workspace RelativeSize="not-a-number" />
            </DockSiteLayout>
            """;
        var node = Assert.IsType<WorkspaceLayoutNode>(Read(xml));
        Assert.Equal(1.0, node.RelativeSize);
    }

    private static LayoutNode? RoundTrip(LayoutNode? node)
    {
        using var stream = new MemoryStream();
        LayoutXml.Write(stream, node);
        stream.Position = 0;
        return LayoutXml.Read(stream);
    }

    private static LayoutNode? Read(string xml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return LayoutXml.Read(stream);
    }
}
