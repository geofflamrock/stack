using FluentAssertions;
using Stack.Commands;
using Stack.Tests.Helpers;

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
    public void MoveBranch_WhenMovingBranchWithoutChildren_MovesBranchToNewLocation()
    {
        // Arrange: Structure:
        // - A
        // - B
        //   - C
        var stack = new Config.Stack(
            "TestStack",
            Some.HttpsUri().ToString(),
            "main",
            [
                new Config.Branch("A", []),
                new Config.Branch("B", [
                    new Config.Branch("C", [])
                ])
            ]
        );

        // Act: Move C from under B to under A
        stack.MoveBranch("C", "A", MoveBranchChildAction.MoveChildren);

        // Assert: Structure should be:
        // - A
        //   - C
        // - B
        stack.Branches.Should().BeEquivalentTo([
            new Config.Branch("A", [new Config.Branch("C", [])]),
            new Config.Branch("B", [])
        ]);
    }

    [Fact]
    public void MoveBranch_WhenMovingBranchToSourceBranch_MovesBranchToRootLevel()
    {
        // Arrange: Structure:
        // - A
        //   - B
        var stack = new Config.Stack(
            "TestStack",
            Some.HttpsUri().ToString(),
            "main",
            [
                new Config.Branch("A", [
                    new Config.Branch("B", [])
                ])
            ]
        );

        // Act: Move B to root level (under source branch)
        stack.MoveBranch("B", "main", MoveBranchChildAction.MoveChildren);

        // Assert: Structure should be:
        // - A
        // - B
        stack.Branches.Should().BeEquivalentTo([
            new Config.Branch("A", []),
            new Config.Branch("B", [])
        ]);
    }

    [Fact]
    public void MoveBranch_WhenMovingBranchWithChildren_AndMoveChildrenAction_MovesBranchWithAllChildren()
    {
        // Arrange: Structure:
        // - A
        // - B
        //   - C
        //     - D
        var stack = new Config.Stack(
            "TestStack",
            Some.HttpsUri().ToString(),
            "main",
            [
                new Config.Branch("A", []),
                new Config.Branch("B", [
                    new Config.Branch("C", [
                        new Config.Branch("D", [])
                    ])
                ])
            ]
        );

        // Act: Move C with its children from under B to under A
        stack.MoveBranch("C", "A", MoveBranchChildAction.MoveChildren);

        // Assert: Structure should be:
        // - A
        //   - C
        //     - D
        // - B
        stack.Branches.Should().BeEquivalentTo([
            new Config.Branch("A", [
                new Config.Branch("C", [
                    new Config.Branch("D", [])
                ])
            ]),
            new Config.Branch("B", [])
        ]);
    }

    [Fact]
    public void MoveBranch_WhenMovingBranchWithChildren_AndReParentChildrenAction_MovesBranchButLeavesChildrenBehind()
    {
        // Arrange: Structure:
        // - A
        // - B
        //   - C
        //     - D
        //     - E
        var stack = new Config.Stack(
            "TestStack",
            Some.HttpsUri().ToString(),
            "main",
            [
                new Config.Branch("A", []),
                new Config.Branch("B", [
                    new Config.Branch("C", [
                        new Config.Branch("D", []),
                        new Config.Branch("E", [])
                    ])
                ])
            ]
        );

        // Act: Move C but re-parent its children to B
        stack.MoveBranch("C", "A", MoveBranchChildAction.ReParentChildren);

        // Assert: Structure should be:
        // - A
        //   - C
        // - B
        //   - D
        //   - E
        stack.Branches.Should().BeEquivalentTo([
            new Config.Branch("A", [
                new Config.Branch("C", [])
            ]),
            new Config.Branch("B", [
                new Config.Branch("D", []),
                new Config.Branch("E", [])
            ])
        ]);
    }

    [Fact]
    public void MoveBranch_WhenMovingDeepNestedBranch_CorrectlyMovesFromAnyDepth()
    {
        // Arrange: Structure:
        // - A
        //   - B
        //     - C
        //       - D
        // - E
        var stack = new Config.Stack(
            "TestStack",
            Some.HttpsUri().ToString(),
            "main",
            [
                new Config.Branch("A", [
                    new Config.Branch("B", [
                        new Config.Branch("C", [
                            new Config.Branch("D", [])
                        ])
                    ])
                ]),
                new Config.Branch("E", [])
            ]
        );

        // Act: Move deeply nested D to under E
        stack.MoveBranch("D", "E", MoveBranchChildAction.MoveChildren);

        // Assert: Structure should be:
        // - A
        //   - B
        //     - C
        // - E
        //   - D
        stack.Branches.Should().BeEquivalentTo([
            new Config.Branch("A", [
                new Config.Branch("B", [
                    new Config.Branch("C", [])
                ])
            ]),
            new Config.Branch("E", [
                new Config.Branch("D", [])
            ])
        ]);
    }

    [Fact]
    public void MoveBranch_WhenBranchNotFound_ThrowsException()
    {
        // Arrange
        var stack = new Config.Stack(
            "TestStack",
            Some.HttpsUri().ToString(),
            "main",
            [
                new Config.Branch("A", [])
            ]
        );

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => stack.MoveBranch("NonExistent", "A", MoveBranchChildAction.MoveChildren));

        exception.Message.Should().Contain("Branch 'NonExistent' not found in stack");
    }

    [Fact]
    public void MoveBranch_WhenNewParentNotFound_ThrowsException()
    {
        // Arrange
        var stack = new Config.Stack(
            "TestStack",
            Some.HttpsUri().ToString(),
            "main",
            [
                new Config.Branch("A", [
                    new Config.Branch("B", [])
                ])
            ]
        );

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => stack.MoveBranch("B", "NonExistent", MoveBranchChildAction.MoveChildren));

        exception.Message.Should().Contain("Parent branch 'NonExistent' not found in stack");
    }

    [Fact]
    public void MoveBranch_WhenMovingRootLevelBranchWithChildrenToAnotherRootLevelBranch_WorksCorrectly()
    {
        // Arrange: Structure:
        // - A
        //   - B
        // - C
        var stack = new Config.Stack(
            "TestStack",
            Some.HttpsUri().ToString(),
            "main",
            [
                new Config.Branch("A", [
                    new Config.Branch("B", [])
                ]),
                new Config.Branch("C", [])
            ]
        );

        // Act: Move A under C
        stack.MoveBranch("A", "C", MoveBranchChildAction.MoveChildren);

        // Assert: Structure should be:
        // - C
        //   - A
        //     - B
        stack.Branches.Should().BeEquivalentTo([
            new Config.Branch("C", [
                new Config.Branch("A", [
                    new Config.Branch("B", [])
                ])
            ])
        ]);
    }
}