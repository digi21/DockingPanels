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
        var xml = """<DockSiteLayout Version="1"><SomethingUnknown /></DockSiteLayout>""";
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

    [Fact]
    public void RoundTrip_AutoHideGroups_ArePreserved()
    {
        var layout = new LayoutDocument { Root = new WorkspaceLayoutNode() };
        var group = new AutoHideGroupNode { Edge = DockSide.Right, Size = 250 };
        group.Windows.Add(new LayoutWindowEntry("a", "AutoHide"));
        group.Windows.Add(new LayoutWindowEntry("b", "AutoHide"));
        layout.AutoHideGroups.Add(group);

        using var stream = new MemoryStream();
        LayoutXml.Write(stream, layout);
        stream.Position = 0;
        var restored = LayoutXml.Read(stream);

        Assert.IsType<WorkspaceLayoutNode>(restored.Root);
        var restoredGroup = Assert.Single(restored.AutoHideGroups);
        Assert.Equal(DockSide.Right, restoredGroup.Edge);
        Assert.Equal(250, restoredGroup.Size);
        Assert.Equal(["a", "b"], restoredGroup.Windows.Select(w => w.Id));
    }

    [Fact]
    public void RoundTrip_AutoHideGroupRestoreHints_ArePreserved()
    {
        var layout = new LayoutDocument { Root = new WorkspaceLayoutNode() };
        var group = new AutoHideGroupNode
        {
            Edge = DockSide.Bottom,
            Size = 180,
            Offset = 241.5,
            RestoreSibling = "Workspace:0",
            RestoreSide = DockSide.Top,
            RestoreRelativeSize = 0.3,
        };
        group.Windows.Add(new LayoutWindowEntry("output", "AutoHide"));
        layout.AutoHideGroups.Add(group);

        using var stream = new MemoryStream();
        LayoutXml.Write(stream, layout);
        stream.Position = 0;
        var restored = LayoutXml.Read(stream);

        var restoredGroup = Assert.Single(restored.AutoHideGroups);
        Assert.Equal(241.5, restoredGroup.Offset);
        Assert.Equal("Workspace:0", restoredGroup.RestoreSibling);
        Assert.Equal(DockSide.Top, restoredGroup.RestoreSide);
        Assert.Equal(0.3, restoredGroup.RestoreRelativeSize);
    }

    [Fact]
    public void Read_AutoHideGroupWithoutRestoreHints_UsesDefaults()
    {
        var xml = """
            <DockSiteLayout Version="1">
              <AutoHideGroup Edge="Bottom" Size="180">
                <ToolWindow Id="output" State="AutoHide" />
              </AutoHideGroup>
            </DockSiteLayout>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var restored = LayoutXml.Read(stream);

        var group = Assert.Single(restored.AutoHideGroups);
        Assert.Equal(0, group.Offset);
        Assert.Null(group.RestoreSibling);
        Assert.Equal(1.0, group.RestoreRelativeSize);
    }

    [Fact]
    public void RoundTrip_FloatingWindows_ArePreserved()
    {
        var pane = new ContainerLayoutNode { SelectedId = "output" };
        pane.Windows.Add(new LayoutWindowEntry("output", "Floating"));
        pane.Windows.Add(new LayoutWindowEntry("watch", "Floating"));

        var layout = new LayoutDocument { Root = new WorkspaceLayoutNode() };
        var floating = new FloatingWindowNode
        {
            X = -1280,
            Y = 240,
            Width = 570,
            Height = 450,
            RestoreContainer = "Window:errorList",
            RestoreSibling = "Workspace:0",
            RestoreSide = DockSide.Bottom,
            RestoreRelativeSize = 0.3,
            Root = pane,
        };
        layout.FloatingWindows.Add(floating);

        using var stream = new MemoryStream();
        LayoutXml.Write(stream, layout);
        stream.Position = 0;
        var restored = LayoutXml.Read(stream);

        Assert.IsType<WorkspaceLayoutNode>(restored.Root);
        var restoredFloating = Assert.Single(restored.FloatingWindows);
        Assert.Equal(-1280, restoredFloating.X);
        Assert.Equal(240, restoredFloating.Y);
        Assert.Equal(570, restoredFloating.Width);
        Assert.Equal(450, restoredFloating.Height);
        Assert.Equal("Window:errorList", restoredFloating.RestoreContainer);
        Assert.Equal("Workspace:0", restoredFloating.RestoreSibling);
        Assert.Equal(DockSide.Bottom, restoredFloating.RestoreSide);
        Assert.Equal(0.3, restoredFloating.RestoreRelativeSize);

        var restoredPane = Assert.IsType<ContainerLayoutNode>(restoredFloating.Root);
        Assert.Equal("output", restoredPane.SelectedId);
        Assert.Equal(["output", "watch"], restoredPane.Windows.Select(w => w.Id));
    }

    [Fact]
    public void RoundTrip_FloatingWindowWithDockedPanes_PreservesItsTree()
    {
        var left = new ContainerLayoutNode { RelativeSize = 0.4, SelectedId = "output" };
        left.Windows.Add(new LayoutWindowEntry("output", "Floating"));
        var right = new ContainerLayoutNode { RelativeSize = 0.6 };
        right.Windows.Add(new LayoutWindowEntry("watch", "Floating"));
        var split = new SplitLayoutNode { Orientation = Orientation.Horizontal };
        split.Children.Add(left);
        split.Children.Add(right);

        var layout = new LayoutDocument();
        layout.FloatingWindows.Add(new FloatingWindowNode { X = 40, Y = 60, Width = 640, Height = 480, Root = split });

        using var stream = new MemoryStream();
        LayoutXml.Write(stream, layout);
        stream.Position = 0;
        var restored = LayoutXml.Read(stream);

        var restoredSplit = Assert.IsType<SplitLayoutNode>(Assert.Single(restored.FloatingWindows).Root);
        Assert.Equal(Orientation.Horizontal, restoredSplit.Orientation);
        Assert.Equal(2, restoredSplit.Children.Count);
        Assert.Equal(0.4, Assert.IsType<ContainerLayoutNode>(restoredSplit.Children[0]).RelativeSize);
        Assert.Equal(
            ["watch"],
            Assert.IsType<ContainerLayoutNode>(restoredSplit.Children[1]).Windows.Select(w => w.Id));
    }

    [Fact]
    public void Read_Version1FloatingWindow_BecomesASinglePane()
    {
        var xml = """
            <DockSiteLayout Version="1">
              <FloatingWindow X="10" Y="20" Width="300" Height="200" SelectedId="watch">
                <ToolWindow Id="output" State="Floating" />
                <ToolWindow Id="watch" State="Floating" />
              </FloatingWindow>
            </DockSiteLayout>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var restored = LayoutXml.Read(stream);

        var pane = Assert.IsType<ContainerLayoutNode>(Assert.Single(restored.FloatingWindows).Root);
        Assert.Equal("watch", pane.SelectedId);
        Assert.Equal(["output", "watch"], pane.Windows.Select(w => w.Id));
    }

    [Fact]
    public void RoundTrip_AutoHideGroupsAndFloatingWindows_Coexist()
    {
        var layout = new LayoutDocument { Root = new WorkspaceLayoutNode() };
        var group = new AutoHideGroupNode { Edge = DockSide.Left, Size = 220 };
        group.Windows.Add(new LayoutWindowEntry("solutionExplorer", "AutoHide"));
        layout.AutoHideGroups.Add(group);
        var pane = new ContainerLayoutNode();
        pane.Windows.Add(new LayoutWindowEntry("output", "Floating"));
        layout.FloatingWindows.Add(new FloatingWindowNode { X = 10, Y = 20, Width = 300, Height = 200, Root = pane });

        using var stream = new MemoryStream();
        LayoutXml.Write(stream, layout);
        stream.Position = 0;
        var restored = LayoutXml.Read(stream);

        Assert.IsType<WorkspaceLayoutNode>(restored.Root);
        Assert.Single(restored.AutoHideGroups);
        Assert.Single(restored.FloatingWindows);
    }

    [Fact]
    public void Read_FloatingWindowWithoutBounds_UsesDefaults()
    {
        var xml = """
            <DockSiteLayout Version="1">
              <FloatingWindow>
                <ToolWindow Id="output" />
              </FloatingWindow>
            </DockSiteLayout>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var restored = LayoutXml.Read(stream);

        var floating = Assert.Single(restored.FloatingWindows);
        Assert.Equal(0, floating.X);
        Assert.Equal(380, floating.Width);
        Assert.Equal(300, floating.Height);
        Assert.Null(floating.RestoreContainer);

        var pane = Assert.IsType<ContainerLayoutNode>(floating.Root);
        Assert.Equal("Floating", Assert.Single(pane.Windows).State);
    }

    [Fact]
    public void RoundTrip_DocumentArea_PreservesTabGroups()
    {
        var left = new DocumentContainerLayoutNode { RelativeSize = 0.6, SelectedId = "readme" };
        left.Windows.Add(new LayoutWindowEntry("readme", "Docked"));
        left.Windows.Add(new LayoutWindowEntry("program", "Docked"));
        var right = new DocumentContainerLayoutNode { RelativeSize = 0.4 };
        right.Windows.Add(new LayoutWindowEntry("notes", "Docked"));
        var groups = new SplitLayoutNode { Orientation = Orientation.Horizontal };
        groups.Children.Add(left);
        groups.Children.Add(right);

        var restored = RoundTrip(new DocumentHostLayoutNode { RelativeSize = 0.7, Root = groups });

        var host = Assert.IsType<DocumentHostLayoutNode>(restored);
        Assert.Equal(0.7, host.RelativeSize);

        var split = Assert.IsType<SplitLayoutNode>(host.Root);
        var restoredLeft = Assert.IsType<DocumentContainerLayoutNode>(split.Children[0]);
        Assert.Equal(0.6, restoredLeft.RelativeSize);
        Assert.Equal("readme", restoredLeft.SelectedId);
        Assert.Equal(["readme", "program"], restoredLeft.Windows.Select(w => w.Id));
        Assert.Equal(
            ["notes"],
            Assert.IsType<DocumentContainerLayoutNode>(split.Children[1]).Windows.Select(w => w.Id));
    }

    [Fact]
    public void RoundTrip_EmptyDocumentArea_HasNoTree()
    {
        var host = Assert.IsType<DocumentHostLayoutNode>(RoundTrip(new DocumentHostLayoutNode()));

        Assert.Null(host.Root);
    }

    [Fact]
    public void RoundTrip_FloatingDocument_IsPreserved()
    {
        var pane = new DocumentContainerLayoutNode { SelectedId = "notes" };
        pane.Windows.Add(new LayoutWindowEntry("notes", "Floating"));

        var layout = new LayoutDocument();
        layout.FloatingWindows.Add(new FloatingWindowNode { X = 30, Y = 40, Width = 500, Height = 400, Root = pane });

        using var stream = new MemoryStream();
        LayoutXml.Write(stream, layout);
        stream.Position = 0;
        var restored = LayoutXml.Read(stream);

        var restoredPane = Assert.IsType<DocumentContainerLayoutNode>(Assert.Single(restored.FloatingWindows).Root);
        Assert.Equal("notes", restoredPane.SelectedId);
        Assert.Equal("Floating", Assert.Single(restoredPane.Windows).State);
    }

    [Fact]
    public void Read_DocumentWithoutId_Throws()
    {
        var xml = """
            <DockSiteLayout Version="3">
              <DocumentHost><DocumentContainer><Document /></DocumentContainer></DocumentHost>
            </DockSiteLayout>
            """;
        Assert.Throws<InvalidDataException>(() => Read(xml));
    }

    [Fact]
    public void Write_UsesTheCurrentFormatVersion()
    {
        using var stream = new MemoryStream();
        LayoutXml.Write(stream, new LayoutDocument());

        Assert.Contains("Version=\"3\"", Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public void RoundTrip_WindowThatCannotAutoHide_KeepsThat()
    {
        var container = new ContainerLayoutNode();
        container.Windows.Add(new LayoutWindowEntry("properties", "Docked", CanAutoHide: false));
        container.Windows.Add(new LayoutWindowEntry("output", "Docked"));

        var restored = Assert.IsType<ContainerLayoutNode>(RoundTrip(container));

        Assert.False(restored.Windows[0].CanAutoHide);
        Assert.Null(restored.Windows[1].CanAutoHide);
    }

    [Fact]
    public void RoundTrip_AutoHideGroupWindowThatCannotAutoHide_KeepsThat()
    {
        var layout = new LayoutDocument { Root = new WorkspaceLayoutNode() };
        var group = new AutoHideGroupNode { Edge = DockSide.Left };
        group.Windows.Add(new LayoutWindowEntry("stranded", "AutoHide", CanAutoHide: false));
        layout.AutoHideGroups.Add(group);

        using var stream = new MemoryStream();
        LayoutXml.Write(stream, layout);
        stream.Position = 0;

        var restored = LayoutXml.Read(stream);
        Assert.False(Assert.Single(Assert.Single(restored.AutoHideGroups).Windows).CanAutoHide);
    }

    [Fact]
    public void Write_WindowThatCanAutoHide_WritesNoAttribute()
    {
        // Files stay byte for byte what earlier versions wrote unless a window actually forbids
        // auto-hiding, so the 1.0 library can still read what this one saves.
        var container = new ContainerLayoutNode();
        container.Windows.Add(new LayoutWindowEntry("output", "Docked"));

        using var stream = new MemoryStream();
        LayoutXml.Write(stream, new LayoutDocument { Root = container });

        Assert.DoesNotContain("CanAutoHide", Encoding.UTF8.GetString(stream.ToArray()));
    }

    [Fact]
    public void Read_WindowWithoutTheAttribute_SaysNothingAboutAutoHiding()
    {
        // What every file saved before the attribute existed looks like: null means the window
        // keeps whatever the application declared, rather than being forced either way.
        var xml = """
            <DockSiteLayout Version="3">
              <ToolWindowContainer><ToolWindow Id="output" State="Docked" /></ToolWindowContainer>
            </DockSiteLayout>
            """;

        var container = Assert.IsType<ContainerLayoutNode>(Read(xml));
        Assert.Null(Assert.Single(container.Windows).CanAutoHide);
    }

    [Fact]
    public void Read_WindowWithAnUnparsableAttribute_SaysNothingAboutAutoHiding()
    {
        var xml = """
            <DockSiteLayout Version="3">
              <ToolWindowContainer><ToolWindow Id="output" State="Docked" CanAutoHide="perhaps" /></ToolWindowContainer>
            </DockSiteLayout>
            """;

        var container = Assert.IsType<ContainerLayoutNode>(Read(xml));
        Assert.Null(Assert.Single(container.Windows).CanAutoHide);
    }

    private static LayoutNode? RoundTrip(LayoutNode? node)
    {
        using var stream = new MemoryStream();
        LayoutXml.Write(stream, new LayoutDocument { Root = node });
        stream.Position = 0;
        return LayoutXml.Read(stream).Root;
    }

    private static LayoutNode? Read(string xml)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        return LayoutXml.Read(stream).Root;
    }
}
