using FluentAssertions;
using Stack.Tests.Helpers;
using Stack.Config;

// Deliberately using a different namespace here to avoid needing to 
// use a fully-qualified name in all other tests.
namespace Stack.Tests;

public class StackTests
{
    [Fact]
    public void GetAllBranchLines_ReturnsAllRootToLeafPaths()
    {
        // Arrange: Build a stack with the following structure:
        // - A
        //   - B
        //     - C
        //     - D
        //   - E
        //   - F
        //     - G
        var stack = new Config.Stack(
            "TestStack",
            Some.HttpsUri().ToString(),
            "main",
            [
                new Config.Branch("A", [
                    new Config.Branch("B", [
                        new Config.Branch("C", []),
                        new Config.Branch("D", [])
                    ]),
                    new Config.Branch("E", []),
                    new Config.Branch("F", [
                        new Config.Branch("G", [])
                    ])
                ])
            ]
        );

        // Act
        var lines = stack.GetAllBranchLines();

        // Assert: Should match the expected root-to-leaf paths (by branch name)
        var branchNameLines = lines.Select(line => line.Select(b => b.Name).ToArray()).ToList();
        branchNameLines.Should().BeEquivalentTo<string[]>(
        [
            ["A", "B", "C"],
            ["A", "B", "D"],
            ["A", "E"],
            ["A", "F", "G"]
        ], options => options.WithStrictOrdering());
    }

    [Fact]
    public void MoveBranch_ToRoot_KeepingChildren_MovesChildrenToOldParent()
    {
        // Stack: A
        //  - B
        //    - C
        // Move B -> root, keep children with old parent => A keeps C, B has no children
        var source = Some.BranchName();
        var A = "A"; var B = "B"; var C = "C";
        var stack = new Config.Stack(
            "Test", Some.HttpsUri().ToString(), source,
            [new Config.Branch(A, [new Config.Branch(B, [new Config.Branch(C, [])])])]);

        stack.MoveBranch(B, source, MoveBranchChildrenAction.KeepChildrenWithOldParent);

        stack.Branches.Select(b => b.Name).Should().BeEquivalentTo([A, B]);
        var a = stack.Branches.First(b => b.Name == A);
        a.Children.Select(c => c.Name).Should().BeEquivalentTo([C]);
        var b = stack.Branches.First(b => b.Name == B);
        b.Children.Should().BeEmpty();
    }

    [Fact]
    public void MoveBranch_UnderAnother_MovingChildren_MovesSubtree()
    {
        // Stack: A
        //  - B
        //  - D
        //    - E
        // Move B under D with children => D -> [E, B]
        var source = Some.BranchName();
        var A = "A"; var B = "B"; var D = "D"; var E = "E";
        var stack = new Config.Stack(
            "Test", Some.HttpsUri().ToString(), source,
            [new Config.Branch(A, [new Config.Branch(B, []), new Config.Branch(D, [new Config.Branch(E, [])])])]);

        stack.MoveBranch(B, D, MoveBranchChildrenAction.MoveChildrenWithBranch);

        var a = stack.Branches.First(b => b.Name == A);
        a.Children.Select(c => c.Name).Should().BeEquivalentTo([D]);
        var d = a.Children.First(c => c.Name == D);
        d.Children.Select(c => c.Name).Should().BeEquivalentTo([E, B]);
    }

    [Fact]
    public void MoveBranch_CannotMoveUnderDescendant()
    {
        var source = Some.BranchName();
        var A = "A"; var B = "B"; var C = "C";
        var stack = new Config.Stack(
            "Test", Some.HttpsUri().ToString(), source,
            [new Config.Branch(A, [new Config.Branch(B, [new Config.Branch(C, [])])])]);

        var act = () => stack.MoveBranch(B, C, MoveBranchChildrenAction.MoveChildrenWithBranch);
        act.Should().Throw<InvalidOperationException>();
    }
}