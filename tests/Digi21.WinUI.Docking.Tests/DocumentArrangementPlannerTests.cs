using Microsoft.UI.Xaml.Controls;
using Xunit;

namespace Digi21.WinUI.Docking.Tests;

// What DocumentHost.Arrange decides before touching a control: which documents land in which group, which groups and
// splits survive, how the space is shared. The documents are strings here; the planner is generic over them.
public class DocumentArrangementPlannerTests
{
    private const string A = "a";
    private const string B = "b";
    private const string C = "c";
    private const string D = "d";
    private const string E = "e";

    private static ArrangementGroup<string> Group(params string[] items) => new(1.0, items, null);

    private static ArrangementSplit<string> Split(Orientation orientation, params ArrangementNode<string>[] children) =>
        new(1.0, orientation, children);

    // Two rows of two.
    private static ArrangementNode<string> Grid(string a, string b, string c, string d) =>
        Split(Orientation.Vertical,
            Split(Orientation.Horizontal, Group(a), Group(b)),
            Split(Orientation.Horizontal, Group(c), Group(d)));

    private static string[][] Groups(ArrangementNode<string>? node) => node switch
    {
        ArrangementGroup<string> group => [[.. group.Items]],
        ArrangementSplit<string> split => [.. split.Children.SelectMany(Groups)],
        _ => [],
    };

    [Fact]
    public void AGridOfFourDocumentsKeepsEveryGroupInOrder()
    {
        var plan = DocumentArrangementPlanner.Plan(Grid(A, B, C, D), []);

        Assert.Equal([[A], [B], [C], [D]], Groups(plan));
        var rows = Assert.IsType<ArrangementSplit<string>>(plan);
        Assert.Equal(Orientation.Vertical, rows.Orientation);
        Assert.All(rows.Children, row => Assert.Equal(Orientation.Horizontal, Assert.IsType<ArrangementSplit<string>>(row).Orientation));
    }

    [Fact]
    public void AGroupLeftEmptyIsDroppedAndASplitOfOneIsReplacedByIt()
    {
        // Three documents for a grid of four: two on top, one across the bottom.
        var plan = DocumentArrangementPlanner.Plan(Split(Orientation.Vertical,
            Split(Orientation.Horizontal, Group(A), Group(B)),
            Split(Orientation.Horizontal, Group(C), Group())), []);

        var rows = Assert.IsType<ArrangementSplit<string>>(plan);
        Assert.Equal(2, rows.Children.Count);
        Assert.IsType<ArrangementSplit<string>>(rows.Children[0]);
        Assert.Equal([C], Assert.IsType<ArrangementGroup<string>>(rows.Children[1]).Items);
    }

    [Fact]
    public void WhenOnlyOneDocumentIsLeftTheResultIsASingleGroup()
    {
        var plan = DocumentArrangementPlanner.Plan(Split(Orientation.Horizontal, Group(A), Group(), Group()), []);

        Assert.Equal(A, Assert.Single(Assert.IsType<ArrangementGroup<string>>(plan).Items));
    }

    [Fact]
    public void NoDocumentsAtAllIsNothingToBuild()
    {
        Assert.Null(DocumentArrangementPlanner.Plan(Split(Orientation.Horizontal, Group(), Group()), []));
    }

    [Fact]
    public void DocumentsTheLayoutDoesNotNameJoinTheLastGroupAfterItsOwn()
    {
        var plan = DocumentArrangementPlanner.Plan(Split(Orientation.Horizontal, Group(A), Group(B)), [A, B, C, D]);

        Assert.Equal([[A], [B, C, D]], Groups(plan));
    }

    [Fact]
    public void ADocumentTheLayoutNamesIsNotAddedAgainAsAnUnlistedOne()
    {
        var plan = DocumentArrangementPlanner.Plan(Group(A, B), [A, B, B, C, C]);

        Assert.Equal([[A, B, C]], Groups(plan));
    }

    [Fact]
    public void ADocumentNamedTwiceIsRefused()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            DocumentArrangementPlanner.Plan(Split(Orientation.Horizontal, Group(A, B), Group(B)), []));

        Assert.Contains("twice", error.Message);
    }

    [Fact]
    public void SiblingsSharingSpaceAreScaledToAddUpToOne()
    {
        var plan = DocumentArrangementPlanner.Plan(new ArrangementSplit<string>(1.0, Orientation.Horizontal,
        [
            new ArrangementGroup<string>(2.0, [A], null),
            new ArrangementGroup<string>(1.0, [B], null),
        ]), []);

        var split = Assert.IsType<ArrangementSplit<string>>(plan);
        Assert.Equal(2.0 / 3.0, split.Children[0].Size, 9);
        Assert.Equal(1.0 / 3.0, split.Children[1].Size, 9);
    }

    [Fact]
    public void WhenAGroupIsDroppedTheOthersShareItsSpace()
    {
        var plan = DocumentArrangementPlanner.Plan(new ArrangementSplit<string>(1.0, Orientation.Horizontal,
        [
            new ArrangementGroup<string>(0.5, [A], null),
            new ArrangementGroup<string>(0.25, [], null),
            new ArrangementGroup<string>(0.25, [B], null),
        ]), []);

        var split = Assert.IsType<ArrangementSplit<string>>(plan);
        Assert.Equal(2, split.Children.Count);
        Assert.Equal(2.0 / 3.0, split.Children[0].Size, 9);
        Assert.Equal(1.0 / 3.0, split.Children[1].Size, 9);
    }

    [Fact]
    public void ASplitReplacedByItsOnlyPaneKeepsTheSplitsShare()
    {
        var plan = DocumentArrangementPlanner.Plan(new ArrangementSplit<string>(1.0, Orientation.Horizontal,
        [
            new ArrangementGroup<string>(0.6, [A], null),
            new ArrangementSplit<string>(0.4, Orientation.Vertical, [new ArrangementGroup<string>(0.5, [B], null), new ArrangementGroup<string>(0.5, [], null)]),
        ]), []);

        var split = Assert.IsType<ArrangementSplit<string>>(plan);
        Assert.Equal([[A], [B]], Groups(plan));
        Assert.Equal(0.6, split.Children[0].Size, 9);
        Assert.Equal(0.4, split.Children[1].Size, 9);
        Assert.IsType<ArrangementGroup<string>>(split.Children[1]);
    }

    [Fact]
    public void TheShownDocumentOfAGroupIsKept()
    {
        var plan = DocumentArrangementPlanner.Plan(new ArrangementGroup<string>(1.0, [A, B, C], B), []);

        Assert.Equal(B, Assert.IsType<ArrangementGroup<string>>(plan).Selected);
    }

    [Fact]
    public void ALayoutWithoutAnyGroupGivesTheUnnamedDocumentsOneOfTheirOwn()
    {
        var plan = DocumentArrangementPlanner.Plan(new ArrangementSplit<string>(1.0, Orientation.Horizontal, []), [A, B]);

        Assert.Equal([A, B], Assert.IsType<ArrangementGroup<string>>(plan).Items);
    }

    [Fact]
    public void ExtraDocumentsOfALargeSetStackUpInTheLastGroupOfAGrid()
    {
        var plan = DocumentArrangementPlanner.Plan(Grid(A, B, C, D), [A, B, C, D, E]);

        Assert.Equal([[A], [B], [C], [D, E]], Groups(plan));
    }

    [Fact]
    public void TheDocumentLayoutBuildersRefuseWhatCannotBeBuilt()
    {
        Assert.Throws<ArgumentNullException>(() => DocumentLayout.Group((DocumentWindow[])null!));
        Assert.Throws<ArgumentNullException>(() => DocumentLayout.Split(Orientation.Horizontal, (DocumentLayout[])null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentLayout.Group().WithRelativeSize(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentLayout.Group().WithRelativeSize(double.NaN));
        Assert.Equal(0.4, DocumentLayout.Group().WithRelativeSize(0.4).RelativeSize);
        Assert.Equal(1.0, DocumentLayout.Group().RelativeSize);
    }
}
