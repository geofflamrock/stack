using FluentAssertions;
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
}