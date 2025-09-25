using FluentAssertions;
using Meziantou.Extensions.Logging.Xunit;
using Stack.Commands.Helpers;
using Stack.Git;
using Xunit.Abstractions;
using Stack.Tests.Helpers;
using Stack.Infrastructure.Settings;

namespace Stack.Tests.Integration;

public class StackActionsTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void PullChanges_WhenChangesExistOnSourceAndBranchInStack_PullsChangesCorrectly()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var otherBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(otherBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Create commits on remote tracking branches to simulate changes to pull
        repo.CreateCommitOnRemoteTrackingBranch(sourceBranch, "Remote change on source");
        repo.CreateCommitOnRemoteTrackingBranch(otherBranch, "Remote change on other");

        // Make source branch current
        repo.ChangeBranch(sourceBranch);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(otherBranch))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        stackActions.PullChanges(stack);

        // Assert - verify that local branches point to the correct SHA from remote branches
        var sourceLocalTip = repo.GetTipOfBranch(sourceBranch);
        var sourceRemoteTip = repo.GetTipOfRemoteBranch(sourceBranch);
        sourceLocalTip.Sha.Should().Be(sourceRemoteTip.Sha, "source branch should be pulled to match remote");

        var otherLocalTip = repo.GetTipOfBranch(otherBranch);
        var otherRemoteTip = repo.GetTipOfRemoteBranch(otherBranch);
        otherLocalTip.Sha.Should().Be(otherRemoteTip.Sha, "other branch should be fetched to match remote");
    }

    [Fact]
    public void PullChanges_WhenChangesExistOnWorktreeBranch_PullsChangesCorrectly()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var worktreeBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(worktreeBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Create commits on remote tracking branch to simulate changes to pull  
        repo.CreateCommitOnRemoteTrackingBranch(worktreeBranch, "Remote change on worktree branch");

        // Switch to source branch first, then create worktree (can't create worktree for current branch)
        repo.ChangeBranch(sourceBranch);

        // Create a worktree for the branch
        var worktreePath = repo.CreateWorktree(worktreeBranch);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(worktreeBranch))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        stackActions.PullChanges(stack);

        // Assert - verify that branch in worktree was pulled correctly
        var worktreeLocalTip = repo.GetTipOfBranch(worktreeBranch);
        var worktreeRemoteTip = repo.GetTipOfRemoteBranch(worktreeBranch);
        worktreeLocalTip.Sha.Should().Be(worktreeRemoteTip.Sha, "worktree branch should be pulled to match remote");
    }

    [Fact]
    public void PullChanges_WhenLocalBranchHasNoRemoteTrackingBranch_DoesNotPullChanges()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var localOnlyBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(localOnlyBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(1)) // No PushToRemote
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        repo.ChangeBranch(sourceBranch);
        var initialLocalTip = repo.GetTipOfBranch(localOnlyBranch);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(localOnlyBranch))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        stackActions.PullChanges(stack);

        // Assert - local-only branch should not have been affected
        var finalLocalTip = repo.GetTipOfBranch(localOnlyBranch);
        finalLocalTip.Sha.Should().Be(initialLocalTip.Sha, "local-only branch should remain unchanged");
    }

    [Fact]
    public void PullChanges_WhenRemoteTrackingBranchIsDeleted_DoesNotPullChanges()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var deletedRemoteBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(deletedRemoteBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Delete the remote tracking branch to simulate deleted remote
        repo.DeleteRemoteTrackingBranch(deletedRemoteBranch);

        repo.ChangeBranch(sourceBranch);
        var initialLocalTip = repo.GetTipOfBranch(deletedRemoteBranch);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(deletedRemoteBranch))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        stackActions.PullChanges(stack);

        // Assert - branch with deleted remote should not be modified
        var finalLocalTip = repo.GetTipOfBranch(deletedRemoteBranch);
        finalLocalTip.Sha.Should().Be(initialLocalTip.Sha, "branch with deleted remote should remain unchanged");
    }

    [Fact]
    public async Task UpdateStack_WhenUpdatingUsingMerge_AndChangesExistOnMultipleBranchLines_UpdatesStackCorrectly()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var line1Branch1 = Some.BranchName();
        var line1Branch2 = Some.BranchName();
        var line2Branch1 = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(line1Branch1).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(line1Branch2).FromSourceBranch(line1Branch1).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(line2Branch1).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Add changes to source branch (simulating changes to merge)
        repo.ChangeBranch(sourceBranch);
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath, "source change");
        repo.Stage(Path.GetFileName(filePath));
        var sourceCommit = repo.Commit("Source branch change");

        // Add changes to line1Branch1 (simulating changes at multiple levels)
        repo.ChangeBranch(line1Branch1);
        var filePath2 = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath2, "line1 change");
        repo.Stage(Path.GetFileName(filePath2));
        var line1Commit = repo.Commit("Line1 branch change");

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(line1Branch1).WithChildBranch(c => c.WithName(line1Branch2)))
            .WithBranch(b => b.WithName(line2Branch1))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Merge, CancellationToken.None);

        // Assert - verify changes were merged through the stack
        var line1Branch2Commits = repo.GetCommitsReachableFromBranch(line1Branch2);
        var line2Branch1Commits = repo.GetCommitsReachableFromBranch(line2Branch1);

        line1Branch2Commits.Should().Contain(c => c.Sha == sourceCommit.Sha, "line1Branch2 should contain source changes");
        line1Branch2Commits.Should().Contain(c => c.Sha == line1Commit.Sha, "line1Branch2 should contain line1Branch1 changes");
        line2Branch1Commits.Should().Contain(c => c.Sha == sourceCommit.Sha, "line2Branch1 should contain source changes");
    }

    [Fact]
    public async Task UpdateStack_WhenUpdatingUsingRebase_AndChangesExistOnMultipleBranchLines_UpdatesStackCorrectly()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var line1Branch1 = Some.BranchName();
        var line1Branch2 = Some.BranchName();
        var line2Branch1 = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(line1Branch1).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(line1Branch2).FromSourceBranch(line1Branch1).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(line2Branch1).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Add changes to source branch (simulating changes to rebase onto)
        repo.ChangeBranch(sourceBranch);
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath, "source change");
        repo.Stage(Path.GetFileName(filePath));
        var sourceCommit = repo.Commit("Source branch change");

        // Add changes to line1Branch1 (simulating changes at multiple levels)
        repo.ChangeBranch(line1Branch1);
        var filePath2 = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath2, "line1 change");
        repo.Stage(Path.GetFileName(filePath2));
        var line1Commit = repo.Commit("Line1 branch change");

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(line1Branch1).WithChildBranch(c => c.WithName(line1Branch2)))
            .WithBranch(b => b.WithName(line2Branch1))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert - verify the rebase operation completed successfully at multiple levels
        var line1Branch2Commits = repo.GetCommitsReachableFromBranch(line1Branch2);
        var line2Branch1Commits = repo.GetCommitsReachableFromBranch(line2Branch1);

        // All branches should contain the source commit after rebase
        line1Branch2Commits.Should().Contain(c => c.Sha == sourceCommit.Sha, "line1Branch2 should contain source changes after rebase");
        line2Branch1Commits.Should().Contain(c => c.Sha == sourceCommit.Sha, "line2Branch1 should contain source changes after rebase");

        // line1Branch2 should also contain the line1Branch1 changes (multi-level rebase)
        // Note: SHA changes during rebase, so check by commit message
        line1Branch2Commits.Should().Contain(c => c.MessageShort == "Line1 branch change", "line1Branch2 should contain line1Branch1 changes after rebase");
    }

    [Fact]
    public async Task UpdateStack_WhenUpdatingUsingMerge_AndBranchCheckedOutInWorktree_UpdatesStackCorrectly()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var parentBranch = Some.BranchName();
        var childBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(parentBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(childBranch).FromSourceBranch(parentBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Add change to source
        repo.ChangeBranch(sourceBranch);
        var sourceFile = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(sourceFile, "source change");
        repo.Stage(Path.GetFileName(sourceFile));
        var sourceCommit = repo.Commit("Source branch change");

        // Add change to parent branch
        repo.ChangeBranch(parentBranch);
        var parentFile = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(parentFile, "parent change");
        repo.Stage(Path.GetFileName(parentFile));
        var parentCommit = repo.Commit("Parent branch change");

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(parentBranch).WithChildBranch(c => c.WithName(childBranch)))
            .Build();

        repo.ChangeBranch(sourceBranch); // ensure not on child
        var worktree = repo.CreateWorktree(childBranch);
        worktree.Should().NotBeNull();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Merge, CancellationToken.None);

        // Assert - child branch should contain both source and parent commits after merge propagated through worktree parent
        var childCommits = repo.GetCommitsReachableFromBranch(childBranch);
        childCommits.Should().Contain(c => c.Sha == sourceCommit.Sha, "child branch should contain source changes after merge");
        childCommits.Should().Contain(c => c.Sha == parentCommit.Sha, "child branch should contain parent branch changes after merge");
    }

    [Fact]
    public async Task UpdateStack_WhenUpdatingUsingRebase_AndBranchCheckedOutInWorktree_UpdatesStackCorrectly()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var parentBranch = Some.BranchName();
        var childBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(parentBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(childBranch).FromSourceBranch(parentBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Add change to source
        repo.ChangeBranch(sourceBranch);
        var sourceFile = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(sourceFile, "source change");
        repo.Stage(Path.GetFileName(sourceFile));
        var sourceCommit = repo.Commit("Source branch change");

        // Add change to parent
        repo.ChangeBranch(parentBranch);
        var parentFile = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(parentFile, "parent change");
        repo.Stage(Path.GetFileName(parentFile));
        repo.Commit("Parent branch change"); // SHA will change during rebase; use message for assertion

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(parentBranch).WithChildBranch(c => c.WithName(childBranch)))
            .Build();

        repo.ChangeBranch(sourceBranch); // ensure not on child
        var worktreePath = repo.CreateWorktree(childBranch);
        worktreePath.Should().NotBeNull();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert - child branch should contain rebased source and parent changes
        var childCommits = repo.GetCommitsReachableFromBranch(childBranch);
        childCommits.Should().Contain(c => c.Sha == sourceCommit.Sha, "child branch should contain source changes after rebase");
        childCommits.Should().Contain(c => c.MessageShort == "Parent branch change", "child branch should contain parent changes after rebase (message match due to rewritten SHA)");
    }

    [Fact]
    public async Task UpdateStack_WhenUpdatingUsingRebase_AndFirstBranchWasSquashMerged_ReparentsOntoSourceBranchToAvoidConflicts()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var secondBranch = Some.BranchName();
        var thirdBranch = Some.BranchName();
        var fourthBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(firstBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(3).PushToRemote())
            .WithBranch(builder => builder.WithName(secondBranch).FromSourceBranch(firstBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithBranch(builder => builder.WithName(thirdBranch).FromSourceBranch(secondBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithBranch(builder => builder.WithName(fourthBranch).FromSourceBranch(secondBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        var tipOfFirstBranch = repo.GetTipOfBranch(firstBranch);

        // Simulate squash merge of the first branch into the source branch on the remote (keeping first branch locally untouched)
        repo.ChangeBranch(sourceBranch);
        var squashCommit = repo.CreateSquashCommitFromBranchOntoBranch(firstBranch, sourceBranch, "Squash merge first branch");

        // Delete the remote tracking branch for firstBranch to simulate PR being closed/merged & branch deleted
        repo.DeleteRemoteTrackingBranch(firstBranch);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(firstBranch)
                .WithChildBranch(c => c.WithName(secondBranch)
                    .WithChildBranch(d => d.WithName(thirdBranch))
                    .WithChildBranch(e => e.WithName(fourthBranch))))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert
        var secondBranchCommits = repo.GetCommitsReachableFromBranch(secondBranch);
        var thirdBranchCommits = repo.GetCommitsReachableFromBranch(thirdBranch);
        var fourthBranchCommits = repo.GetCommitsReachableFromBranch(fourthBranch);

        secondBranchCommits.Should().Contain(c => c.Sha == squashCommit.Sha, "Second branch should contain squash commit from source");
        thirdBranchCommits.Should().Contain(c => c.Sha == squashCommit.Sha, "Third branch should contain squash commit from source");
        fourthBranchCommits.Should().Contain(c => c.Sha == squashCommit.Sha, "Fourth branch should contain squash commit from source");
        secondBranchCommits.Should().NotContain(c => c.Sha == tipOfFirstBranch.Sha, "Second branch should not contain tip commit from first branch");
        thirdBranchCommits.Should().NotContain(c => c.Sha == tipOfFirstBranch.Sha, "Third branch should not contain tip commit from first branch");
        fourthBranchCommits.Should().NotContain(c => c.Sha == tipOfFirstBranch.Sha, "Fourth branch should not contain tip commit from first branch");

        repo.CreateCommitOnRemoteTrackingBranch(sourceBranch, "New commit on source after squash merge");
        var tipOfSourceAfterNewCommit = repo.GetTipOfBranch(sourceBranch);

        // Act again to verify subsequent rebase still works
        await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        secondBranchCommits = repo.GetCommitsReachableFromBranch(secondBranch);
        thirdBranchCommits = repo.GetCommitsReachableFromBranch(thirdBranch);
        fourthBranchCommits = repo.GetCommitsReachableFromBranch(fourthBranch);
        secondBranchCommits.Should().Contain(c => c.Sha == tipOfSourceAfterNewCommit.Sha, "Second branch should contain latest commit from source after subsequent rebase");
        thirdBranchCommits.Should().Contain(c => c.Sha == tipOfSourceAfterNewCommit.Sha, "Third branch should contain latest commit from source after subsequent rebase");
        fourthBranchCommits.Should().Contain(c => c.Sha == tipOfSourceAfterNewCommit.Sha, "Fourth branch should contain latest commit from source after subsequent rebase");
    }

    [Fact]
    public async Task UpdateStack_WhenUpdatingUsingRebase_AndFirstBranchWasSquashMergedWithAdditionalCommitsNotMergedIntoChildren_ReparentsOntoSourceBranchToAvoidConflicts()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var secondBranch = Some.BranchName();
        var thirdBranch = Some.BranchName();
        var fourthBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(firstBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(3).PushToRemote())
            .WithBranch(builder => builder.WithName(secondBranch).FromSourceBranch(firstBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithBranch(builder => builder.WithName(thirdBranch).FromSourceBranch(secondBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithBranch(builder => builder.WithName(fourthBranch).FromSourceBranch(secondBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Add another commit to the first branch that isn't in the children
        repo.ChangeBranch(firstBranch);
        repo.Commit("file.txt", Some.Name(), "Additional commit on first branch not in children");
        repo.Push(firstBranch);

        var tipOfFirstBranch = repo.GetTipOfBranch(firstBranch);

        // Simulate squash merge of the first branch into the source branch on the remote (keeping first branch locally untouched)
        repo.ChangeBranch(sourceBranch);
        var squashCommit = repo.CreateSquashCommitFromBranchOntoBranch(firstBranch, sourceBranch, "Squash merge first branch");

        // Delete the remote tracking branch for firstBranch to simulate PR being closed/merged & branch deleted
        repo.DeleteRemoteTrackingBranch(firstBranch);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(firstBranch)
                .WithChildBranch(c => c.WithName(secondBranch)
                    .WithChildBranch(d => d.WithName(thirdBranch))
                    .WithChildBranch(e => e.WithName(fourthBranch))))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert
        var secondBranchCommits = repo.GetCommitsReachableFromBranch(secondBranch);
        var thirdBranchCommits = repo.GetCommitsReachableFromBranch(thirdBranch);
        var fourthBranchCommits = repo.GetCommitsReachableFromBranch(fourthBranch);

        secondBranchCommits.Should().Contain(c => c.Sha == squashCommit.Sha, "Second branch should contain squash commit from source");
        thirdBranchCommits.Should().Contain(c => c.Sha == squashCommit.Sha, "Third branch should contain squash commit from source");
        fourthBranchCommits.Should().Contain(c => c.Sha == squashCommit.Sha, "Fourth branch should contain squash commit from source");
        secondBranchCommits.Should().NotContain(c => c.Sha == tipOfFirstBranch.Sha, "Second branch should not contain tip commit from first branch");
        thirdBranchCommits.Should().NotContain(c => c.Sha == tipOfFirstBranch.Sha, "Third branch should not contain tip commit from first branch");
        fourthBranchCommits.Should().NotContain(c => c.Sha == tipOfFirstBranch.Sha, "Fourth branch should not contain tip commit from first branch");
    }

    [Fact]
    public void PushChanges_WhenChangesExistOnCurrentBranch_PushesChangesCorrectly()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var currentBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(currentBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Make changes to the current branch
        repo.ChangeBranch(currentBranch);
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath, "local change");
        repo.Stage(Path.GetFileName(filePath));
        var localCommit = repo.Commit("Local change on current branch");

        var initialRemoteCommitCount = repo.GetCommitsReachableFromRemoteBranch(currentBranch).Count;

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(currentBranch))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        stackActions.PushChanges(stack, 5, false);

        // Assert - remote branch should be at same SHA as local branch
        var localTip = repo.GetTipOfBranch(currentBranch);
        var remoteTip = repo.GetTipOfRemoteBranch(currentBranch);
        remoteTip.Sha.Should().Be(localTip.Sha, "remote branch should be at same SHA as local branch after push");
    }

    [Fact]
    public void PushChanges_WhenChangesExistOnNonCurrentBranch_PushesChangesCorrectly()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var nonCurrentBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(nonCurrentBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Make changes to the non-current branch
        repo.ChangeBranch(nonCurrentBranch);
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath, "local change");
        repo.Stage(Path.GetFileName(filePath));
        var localCommit = repo.Commit("Local change on non-current branch");

        // Switch to source branch so nonCurrentBranch is non-current
        repo.ChangeBranch(sourceBranch);
        var initialRemoteCommitCount = repo.GetCommitsReachableFromRemoteBranch(nonCurrentBranch).Count;

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(nonCurrentBranch))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        stackActions.PushChanges(stack, 5, false);

        // Assert - remote branch should be at same SHA as local branch after push
        var localTip = repo.GetTipOfBranch(nonCurrentBranch);
        var remoteTip = repo.GetTipOfRemoteBranch(nonCurrentBranch);
        remoteTip.Sha.Should().Be(localTip.Sha, "remote branch should match local branch SHA after push");
    }

    [Fact]
    public void PushChanges_WhenChangesExistOnWorktreeBranch_PushesChangesCorrectly()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var worktreeBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(worktreeBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Make changes to the worktree branch, then switch away to create worktree
        repo.ChangeBranch(worktreeBranch);
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath, "worktree branch change");
        repo.Stage(Path.GetFileName(filePath));
        var branchCommit = repo.Commit("Change in worktree branch");

        // Switch to source branch first, then create worktree (can't create worktree for current branch)
        repo.ChangeBranch(sourceBranch);

        // Create a worktree for the branch
        var worktreePath = repo.CreateWorktree(worktreeBranch);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(worktreeBranch))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act
        stackActions.PushChanges(stack, 5, false);

        // Assert - verify that branch in worktree was pushed correctly
        var worktreeLocalTip = repo.GetTipOfBranch(worktreeBranch);
        var worktreeRemoteTip = repo.GetTipOfRemoteBranch(worktreeBranch);
        worktreeRemoteTip.Sha.Should().Be(worktreeLocalTip.Sha, "worktree branch should be pushed to match local branch");
    }

    [Fact]
    public void PushChanges_WhenLocalOnlyBranchExists_CreatesRemoteTrackingBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var localOnlyBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(localOnlyBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(1)) // No PushToRemote
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Make changes to the local-only branch
        repo.ChangeBranch(localOnlyBranch);
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath, "local change");
        repo.Stage(Path.GetFileName(filePath));
        var localCommit = repo.Commit("Local change on local-only branch");

        repo.ChangeBranch(sourceBranch);

        // Verify initially no remote tracking branch
        var initialHasRemoteTracking = repo.DoesRemoteBranchExist(localOnlyBranch);
        initialHasRemoteTracking.Should().BeFalse("local-only branch should initially not have remote tracking branch");

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(localOnlyBranch))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act - should complete without errors and should actually create the remote tracking branch
        stackActions.PushChanges(stack, 5, false);

        // Assert - verify branch exists locally and now HAS a remote tracking branch (because PushChanges creates it)
        var tipOfBranch = repo.GetTipOfBranch(localOnlyBranch); // Throws if branch doesn't exist
        var finalHasRemoteTracking = repo.DoesRemoteBranchExist(localOnlyBranch);

        finalHasRemoteTracking.Should().BeTrue("PushChanges should create remote tracking branch for branches without one");

        // Also assert that the remote branch has the correct SHA from the local branch
        var localTip = repo.GetTipOfBranch(localOnlyBranch);
        var remoteTip = repo.GetTipOfRemoteBranch(localOnlyBranch);
        remoteTip.Sha.Should().Be(localTip.Sha, "remote branch should have same SHA as local branch");
    }

    [Fact]
    public void PushChanges_WhenRemoteTrackingBranchHasBeenDeleted_DoesNotPushChanges()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var deletedRemoteBranch = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .WithBranch(builder => builder.WithName(deletedRemoteBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var cliExecutionContext = new CliExecutionContext() { WorkingDirectory = repo.LocalDirectoryPath };
        var gitClientFactory = new TestGitClientFactory(testOutputHelper);
        var conflictResolutionDetector = new ConflictResolutionDetector();

        // Make changes to the branch
        repo.ChangeBranch(deletedRemoteBranch);
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath, "local change");
        repo.Stage(Path.GetFileName(filePath));
        var localCommit = repo.Commit("Local change on branch");

        // Delete the remote tracking branch to simulate deleted remote
        repo.DeleteRemoteTrackingBranch(deletedRemoteBranch);

        repo.ChangeBranch(sourceBranch);
        var initialLocalCommitCount = repo.GetCommitsReachableFromBranch(deletedRemoteBranch).Count;

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(deletedRemoteBranch))
            .Build();

        var stackActions = new StackActions(gitClientFactory, cliExecutionContext, logger, displayProvider, conflictResolutionDetector);

        // Act - should complete without errors even with deleted remote
        stackActions.PushChanges(stack, 5, false);

        // Assert - local branch should retain its changes
        var finalLocalCommitCount = repo.GetCommitsReachableFromBranch(deletedRemoteBranch).Count;
        finalLocalCommitCount.Should().Be(initialLocalCommitCount, "local branch should retain its changes");

        // Verify that attempting to get the remote branch tip throws (because remote doesn't exist)
        var act = () => repo.GetTipOfRemoteBranch(deletedRemoteBranch);
        act.Should().Throw<Exception>("remote branch should not be accessible after deletion");
    }
}

public class TestGitClientFactory(ITestOutputHelper testOutputHelper) : IGitClientFactory
{
    private readonly Dictionary<string, GitClient> gitClients = [];

    public IGitClient Create(string workingDirectory)
    {
        if (!gitClients.ContainsKey(workingDirectory))
        {
            var logger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            gitClients[workingDirectory] = new GitClient(logger, workingDirectory);
        }
        return gitClients[workingDirectory];
    }
}