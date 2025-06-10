using FluentAssertions;
using Stack.Tests.Helpers;

// Deliberately using a different namespace here to avoid needing to 
// use a fully-qualified name in all other tests.
namespace Stack.Tests;

public class StackTests
{
    [Fact]
    public void GetDefaultBranchName_WhenNoBranchesInStack_ShouldReturnBranchNameWithTheNumber1AtTheEnd_BecauseItIsTheFirstBranch()
    {
        // Arrange
        var stack = new Config.Stack("Test Stack", Some.HttpsUri().ToString(), "branch-1", []);

        // Act
        var branch = stack.GetDefaultBranchName();

        // Assert
        branch.Should().Be("test-stack-1");
    }

    [Fact]
    public void GetDefaultBranchName_WhenMultipleBranchesInStack_ShouldReturnBranchNameWithCorrectNumberAtTheEnd()
    {
        // Arrange
        var stack = new Config.Stack(
            "Test Stack",
            Some.HttpsUri().ToString(),
            "branch-1",
            [
                new Config.Branch("branch-2", [new Config.Branch("branch-3", [])]),
            ]);

        // Act
        var branch = stack.GetDefaultBranchName();

        // Assert
        branch.Should().Be("test-stack-3");
    }
}