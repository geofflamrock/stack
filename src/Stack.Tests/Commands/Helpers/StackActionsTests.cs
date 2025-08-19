using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Infrastructure;
using Xunit.Abstractions;

namespace Stack.Tests.Helpers;

public class StackActionsTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void PullChanges_WhenSomeBranchesHaveChanges_AndOthersDoNot_OnlyPullsChangesForBranchesThatNeedIt()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchWithRemoteChanges = Some.BranchName();
        var branchWithoutRemoteChanges = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();

        var branchStatus = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branchWithRemoteChanges, new GitBranchStatus(branchWithRemoteChanges, $"origin/{branchWithRemoteChanges}", true, false, 0, 3, new Commit(Some.Sha(), Some.Name())) },
            { branchWithoutRemoteChanges, new GitBranchStatus(branchWithoutRemoteChanges, $"origin/{branchWithoutRemoteChanges}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        };

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatus);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchWithRemoteChanges))
            .WithBranch(b => b.WithName(branchWithoutRemoteChanges))
            .Build();

        var stackActions = new StackActions(gitClient, new TestLogger(testOutputHelper));

        // Act
        stackActions.PullChanges(stack);

        // Assert
        gitClient.DidNotReceive().PullBranch(sourceBranch);
        gitClient.Received().PullBranch(branchWithRemoteChanges);
        gitClient.DidNotReceive().PullBranch(branchWithoutRemoteChanges);
    }

    [Fact]
    public void PullChanges_WhenSomeBranchesDoNotExistInRemote_OnlyPullsBranchesThatExistInRemote()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchThatExistsInRemote = Some.BranchName();
        var branchThatDoesNotExistInRemote = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();

        var branchStatus = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branchThatExistsInRemote, new GitBranchStatus(branchThatExistsInRemote, $"origin/{branchThatExistsInRemote}", true, false, 0, 2, new Commit(Some.Sha(), Some.Name())) },
            { branchThatDoesNotExistInRemote, new GitBranchStatus(branchThatDoesNotExistInRemote, $"origin/{branchThatDoesNotExistInRemote}", false, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        };

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatus);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchThatExistsInRemote))
            .WithBranch(b => b.WithName(branchThatDoesNotExistInRemote))
            .Build();

        var stackActions = new StackActions(gitClient, new TestLogger(testOutputHelper));

        // Act
        stackActions.PullChanges(stack);

        // Assert
        gitClient.DidNotReceive().PullBranch(sourceBranch);
        gitClient.Received().PullBranch(branchThatExistsInRemote);
        gitClient.DidNotReceive().PullBranch(branchThatDoesNotExistInRemote);
    }
}