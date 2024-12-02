using FluentAssertions;
using Stack.Tests.Helpers;

namespace Stack.Tests.Models;

public class StackExtensionMethodTests
{
    [Fact]
    public void GetDefaultBranchName_WhenNoBranchesInStack_ShouldReturnBranchNameWithTheNumber1AtTheEnd_BecauseItIsTheFirstBranch()
    {
        // Arrange
        var stack = new Stack.Models.Stack("Test Stack", Some.HttpsUri().ToString(), "branch-1", []);

        // Act
        var branch = stack.GetDefaultBranchName();

        // Assert
        branch.Should().Be("test-stack-1");
    }

    [Fact]
    public void GetDefaultBranchName_WhenMultipleBranchesInStack_ShouldReturnBranchNameWithCorrectNumberAtTheEnd()
    {
        // Arrange
        var stack = new Stack.Models.Stack("Test Stack", Some.HttpsUri().ToString(), "branch-1", ["branch-2", "branch-3"]);

        // Act
        var branch = stack.GetDefaultBranchName();

        // Assert
        branch.Should().Be("test-stack-3");
    }
}