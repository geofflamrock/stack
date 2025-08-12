using FluentAssertions;
using Stack.Git;

namespace Stack.Tests.Git;

public class GitBranchStatusParserTests
{
    [Fact]
    public void WhenBranchIsCurrentBranch_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "* main 1234567 [origin/main] Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", "origin/main", true, true, 0, 0, new Commit("1234567", "Some message")));
    }

    [Fact]
    public void WhenBranchIsAheadAndBehindItsRemoteTrackingBranch_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "  main 1234567 [origin/main: ahead 1, behind 2] Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", "origin/main", true, false, 1, 2, new Commit("1234567", "Some message")));
    }

    [Fact]
    public void WhenBranchIsAheadOfItsRemoteTrackingBranch_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "  main 1234567 [origin/main: ahead 1] Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", "origin/main", true, false, 1, 0, new Commit("1234567", "Some message")));
    }

    [Fact]
    public void WhenBranchIsBehindItsRemoteTrackingBranch_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "  main 1234567 [origin/main: behind 2] Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", "origin/main", true, false, 0, 2, new Commit("1234567", "Some message")));
    }

    [Fact]
    public void WhenBranchIsNotTracked_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "  main 1234567 Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", null, false, false, 0, 0, new Commit("1234567", "Some message")));
    }

    [Fact]
    public void WhenRemoteTrackingBranchIsGone_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "  main 1234567 [origin/main: gone] Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", "origin/main", false, false, 0, 0, new Commit("1234567", "Some message")));
    }

    [Fact]
    public void WhenBranchHasWorktreePathWithoutStatus_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "+ main 1234567 (D:/path/to/worktree) [origin/main] Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", "origin/main", true, false, 0, 0, new Commit("1234567", "Some message")));
    }

    [Fact]
    public void WhenBranchHasWorktreePathWithBehindStatus_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "+ main 1234567 (D:/path/to/worktree) [origin/main: behind 6] Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", "origin/main", true, false, 0, 6, new Commit("1234567", "Some message")));
    }

    [Fact]
    public void WhenBranchHasWorktreePathWithAheadStatus_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "+ main 1234567 (D:/path/to/worktree) [origin/main: ahead 6] Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", "origin/main", true, false, 6, 0, new Commit("1234567", "Some message")));
    }

    [Fact]
    public void WhenBranchHasWorktreePathWithAheadAndBehindStatus_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "+ main 1234567 (D:/path/to/worktree) [origin/main: ahead 6, behind 2] Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", "origin/main", true, false, 6, 2, new Commit("1234567", "Some message")));
    }

    [Fact]
    public void WhenBranchHasWorktreePath_AndRemoteBranchIsGone_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "+ main 1234567 (D:/path/to/worktree) [origin/main: gone] Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", "origin/main", false, false, 0, 0, new Commit("1234567", "Some message")));
    }

    [Fact]
    public void WhenBranchHasWorktreePath_AndBranchIsNotTracked_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "+ main 1234567 (D:/path/to/worktree) Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", null, false, false, 0, 0, new Commit("1234567", "Some message")));
    }

    [Fact]
    public void WhenBranchHasWorktreePathWithParentheses_ReturnsCorrectStatus()
    {
        // Arrange
        var branchStatus = "+ main 1234567 (D:/path/with (parentheses)/worktree) [origin/main: ahead 3, behind 1] Some message";

        // Act
        var result = GitBranchStatusParser.Parse(branchStatus);

        // Assert
        result.Should().Be(new GitBranchStatus("main", "origin/main", true, false, 3, 1, new Commit("1234567", "Some message")));
    }
}
