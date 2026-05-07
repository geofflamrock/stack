using FluentAssertions;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Helpers;

public class StackHelpersTests
{
    private static BranchDetail ActiveBranch(string name, List<BranchDetail>? children = null) =>
        new(name, true, new Commit(Some.Sha(), Some.Name()),
            new RemoteTrackingBranchStatus($"origin/{name}", true, 0, 0),
            null, null, children ?? []);

    private static BranchDetail InactiveBranch(string name, List<BranchDetail>? children = null) =>
        new(name, true, new Commit(Some.Sha(), Some.Name()),
            new RemoteTrackingBranchStatus($"origin/{name}", false, 0, 0),
            null, null, children ?? []);

    private static BranchDetail MissingBranch(string name, List<BranchDetail>? children = null) =>
        new(name, false, null, null, null, null, children ?? []);

    [Fact]
    public void WhenAllBranchesAreActive_ReturnsUnchangedStructure()
    {
        // Arrange
        var branch1 = ActiveBranch("branch-1");
        var branch2 = ActiveBranch("branch-2", [branch1]);
        var branches = new List<BranchDetail> { branch2 };

        // Act
        var result = StackHelpers.GetBranchesForDisplay(branches);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("branch-2");
        result[0].Children.Should().HaveCount(1);
        result[0].Children[0].Name.Should().Be("branch-1");
    }

    [Fact]
    public void WhenAnInactiveBranchHasAnActiveChild_InactiveBranchIsMovedToEnd_ActiveChildIsPromoted()
    {
        // Arrange
        var activeChild = ActiveBranch("active-child");
        var inactiveParent = InactiveBranch("inactive-parent", [activeChild]);
        var branches = new List<BranchDetail> { inactiveParent };

        // Act
        var result = StackHelpers.GetBranchesForDisplay(branches);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("active-child");
        result[0].IsActive.Should().BeTrue();
        result[1].Name.Should().Be("inactive-parent");
        result[1].IsActive.Should().BeFalse();
        result[1].Children.Should().BeEmpty();
    }

    [Fact]
    public void WhenAnInactiveBranchHasNoActiveChildren_OnlyInactiveBranchIsAtEnd()
    {
        // Arrange
        var inactiveBranch = InactiveBranch("inactive");
        var branches = new List<BranchDetail> { inactiveBranch };

        // Act
        var result = StackHelpers.GetBranchesForDisplay(branches);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("inactive");
        result[0].IsActive.Should().BeFalse();
    }

    [Fact]
    public void WhenActiveBranchHasAnInactiveChildWithAnActiveGrandchild_InactiveChildIsMovedToEnd_ActiveGrandchildBecomesChildOfActiveBranch()
    {
        // Arrange
        var activeGrandchild = ActiveBranch("active-grandchild");
        var inactiveChild = InactiveBranch("inactive-child", [activeGrandchild]);
        var activeBranch = ActiveBranch("active-branch", [inactiveChild]);
        var branches = new List<BranchDetail> { activeBranch };

        // Act
        var result = StackHelpers.GetBranchesForDisplay(branches);

        // Assert - active-branch should have active-grandchild as a direct child
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("active-branch");
        result[0].IsActive.Should().BeTrue();
        result[0].Children.Should().HaveCount(1);
        result[0].Children[0].Name.Should().Be("active-grandchild");
        // inactive-child is moved to end with no children
        result[1].Name.Should().Be("inactive-child");
        result[1].IsActive.Should().BeFalse();
        result[1].Children.Should().BeEmpty();
    }

    [Fact]
    public void WhenMixedActiveAndInactiveBranches_ActiveOnesAreFirstInactiveLast()
    {
        // Arrange
        var active1 = ActiveBranch("active-1");
        var active2 = ActiveBranch("active-2");
        var inactive1 = InactiveBranch("inactive-1");
        var inactive2 = InactiveBranch("inactive-2");
        var branches = new List<BranchDetail> { inactive1, active1, inactive2, active2 };

        // Act
        var result = StackHelpers.GetBranchesForDisplay(branches);

        // Assert
        result.Should().HaveCount(4);
        result[0].Name.Should().Be("active-1");
        result[1].Name.Should().Be("active-2");
        result[2].Name.Should().Be("inactive-1");
        result[3].Name.Should().Be("inactive-2");
    }

    [Fact]
    public void WhenBranchDoesNotExistLocally_IsTreatedAsInactive_ActiveChildIsPromoted()
    {
        // Arrange
        var activeChild = ActiveBranch("active-child");
        var missingBranch = MissingBranch("missing-branch", [activeChild]);
        var branches = new List<BranchDetail> { missingBranch };

        // Act
        var result = StackHelpers.GetBranchesForDisplay(branches);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("active-child");
        result[0].IsActive.Should().BeTrue();
        result[1].Name.Should().Be("missing-branch");
        result[1].IsActive.Should().BeFalse();
        result[1].Children.Should().BeEmpty();
    }

    [Fact]
    public void WhenDeeplyNestedInactiveBranches_AllInactiveBranchesAreAtEnd_AllActiveChildrenArePromoted()
    {
        // Arrange
        // Structure: A(active) -> B(inactive) -> C(active) -> D(inactive) -> E(active)
        var e = ActiveBranch("E");
        var d = InactiveBranch("D", [e]);
        var c = ActiveBranch("C", [d]);
        var b = InactiveBranch("B", [c]);
        var a = ActiveBranch("A", [b]);
        var branches = new List<BranchDetail> { a };

        // Act
        var result = StackHelpers.GetBranchesForDisplay(branches);

        // Assert
        // A should have C as child (B was inactive, its active child C is promoted)
        // C should have E as child (D was inactive, its active child E is promoted)
        // B and D should be at the end with no children
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("A");
        result[0].Children.Should().HaveCount(1);
        result[0].Children[0].Name.Should().Be("C");
        result[0].Children[0].Children.Should().HaveCount(1);
        result[0].Children[0].Children[0].Name.Should().Be("E");
        result[1].Name.Should().Be("B");
        result[1].Children.Should().BeEmpty();
        result[2].Name.Should().Be("D");
        result[2].Children.Should().BeEmpty();
    }

    [Fact]
    public void WhenActiveBranchHasAllInactiveChildren_ActiveBranchHasNoChildren_InactiveChildrenAtEnd()
    {
        // Arrange
        var inactive1 = InactiveBranch("inactive-1");
        var inactive2 = InactiveBranch("inactive-2");
        var activeBranch = ActiveBranch("active-branch", [inactive1, inactive2]);
        var branches = new List<BranchDetail> { activeBranch };

        // Act
        var result = StackHelpers.GetBranchesForDisplay(branches);

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("active-branch");
        result[0].Children.Should().BeEmpty();
        result[1].Name.Should().Be("inactive-1");
        result[2].Name.Should().Be("inactive-2");
    }

    [Fact]
    public void WhenInactiveBranchHasAllInactiveChildren_AllInactiveBranchesAreAtEnd()
    {
        // Arrange
        var inactiveChild1 = InactiveBranch("inactive-child-1");
        var inactiveChild2 = InactiveBranch("inactive-child-2");
        var inactiveParent = InactiveBranch("inactive-parent", [inactiveChild1, inactiveChild2]);
        var branches = new List<BranchDetail> { inactiveParent };

        // Act
        var result = StackHelpers.GetBranchesForDisplay(branches);

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("inactive-parent");
        result[0].Children.Should().BeEmpty();
        result[1].Name.Should().Be("inactive-child-1");
        result[2].Name.Should().Be("inactive-child-2");
    }
}
