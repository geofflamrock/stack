using FluentAssertions;
using NSubstitute;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Infrastructure;
using Xunit.Abstractions;

namespace Stack.Tests.Helpers;

public class StackHelpersTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void UpdateStack_WhenThereAreConflictsMergingBranches_AndUpdateIsContinued_TheUpdateCompletesSuccessfully()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var inputProvider = Substitute.For<IInputProvider>();

        var stack = new Config.Stack("Stack1", Some.HttpsUri().ToString(), sourceBranch, [branch1, branch2]);
        var branchDetail1 = new BranchDetail
        {
            Status = new BranchStatus(true, true, true, false, 0, 0, 0, 0, null)
        };
        var branchDetail2 = new BranchDetail
        {
            Status = new BranchStatus(true, true, true, false, 0, 0, 0, 0, null)
        };

        var stackStatus = new StackStatus(new Dictionary<string, BranchDetail>
        {
            { branch1, branchDetail1 },
            { branch2, branchDetail2 },
        });

        inputProvider
            .Select(
                Questions.ContinueOrAbortMerge,
                Arg.Any<MergeConflictAction[]>(),
                Arg.Any<Func<MergeConflictAction, string>>())
            .Returns(MergeConflictAction.Continue);

        gitClient
            .When(g => g.MergeFromLocalSourceBranch(sourceBranch))
            .Throws(new ConflictException());

        // Act
        StackHelpers.UpdateStack(
            stack,
            stackStatus,
            UpdateStrategy.Merge,
            gitClient,
            inputProvider,
            logger
        );

        // Assert
        gitClient.Received().ChangeBranch(branch2);
        gitClient.Received().MergeFromLocalSourceBranch(branch1);
    }

    [Fact]
    public void UpdateStack_WhenThereAreConflictsMergingBranches_AndUpdateIsAborted_AnExceptionIsThrown()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var logger = new TestLogger(testOutputHelper);

        var stack = new Config.Stack("Stack1", Some.HttpsUri().ToString(), sourceBranch, [branch1, branch2]);
        var branchDetail1 = new BranchDetail
        {
            Status = new BranchStatus(true, true, true, false, 0, 0, 0, 0, null)
        };
        var branchDetail2 = new BranchDetail
        {
            Status = new BranchStatus(true, true, true, false, 0, 0, 0, 0, null)
        };

        var stackStatus = new StackStatus(new Dictionary<string, BranchDetail>
        {
            { branch1, branchDetail1 },
            { branch2, branchDetail2 },
        });

        gitClient
            .When(g => g.MergeFromLocalSourceBranch(sourceBranch))
            .Throws(new ConflictException());

        inputProvider
            .Select(
                Questions.ContinueOrAbortMerge,
                Arg.Any<MergeConflictAction[]>(),
                Arg.Any<Func<MergeConflictAction, string>>())
            .Returns(MergeConflictAction.Abort);

        // Act
        var updateAction = () => StackHelpers.UpdateStack(
            stack,
            stackStatus,
            UpdateStrategy.Merge,
            gitClient,
            inputProvider,
            logger
        );

        // Assert
        updateAction.Should().Throw<Exception>().WithMessage("Merge aborted due to conflicts.");
        gitClient.Received().AbortMerge();
        gitClient.DidNotReceive().ChangeBranch(branch2);
        gitClient.DidNotReceive().MergeFromLocalSourceBranch(branch1);
    }

    [Fact]
    public void UpdateStackUsingRebase_WhenARemoteBranchIsDeleted_RebasesOntoTheParentBranchToAvoidConflicts()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var changedFilePath = Some.Name();
        var commit1ChangedFileContents = "These are the changes in the first commit";
        var commit2ChangedFileContents = "These are the changes in the first commit, with some additional changes in the second commit";

        // We have three branches in this scenario:
        //
        // sourceBranch: A commit containing the changes that were made in branch1 but with a different hash e.g. a squash merge
        // branch1: A commit containing the original changes to the file
        // branch2: A second commit that changes the file again, building on the one from branch1
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b
                .WithName(sourceBranch)
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch1)
                .FromSourceBranch(sourceBranch)
                .WithCommit(c => c.WithChanges(changedFilePath, commit1ChangedFileContents))
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch2)
                .FromSourceBranch(branch1)
                .WithCommit(c => c.WithChanges(changedFilePath, commit2ChangedFileContents))
                .PushToRemote())
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = new GitClient(new TestLogger(testOutputHelper), repo.GitClientSettings);
        var logger = new TestLogger(testOutputHelper);
        var gitHubClient = Substitute.For<IGitHubClient>();

        gitClient.ChangeBranch(sourceBranch);
        File.WriteAllText(Path.Join(repo.LocalDirectoryPath, changedFilePath), commit1ChangedFileContents);
        repo.Stage(changedFilePath);
        repo.Commit();
        repo.Push(sourceBranch);

        // Delete the remote branch for branch1 to simulate a PR being merged
        repo.DeleteRemoteTrackingBranch(branch1);

        var stack = new Config.Stack("Stack1", Some.HttpsUri().ToString(), sourceBranch, [branch1, branch2]);
        var stackStatus = StackHelpers.GetStackStatus(stack, branch1, logger, gitClient, gitHubClient, false);

        // Act
        StackHelpers.UpdateStackUsingRebase(stack, stackStatus, gitClient, inputProvider, logger);

        // Assert
        gitClient.ChangeBranch(branch2);
        var fileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, changedFilePath));
        fileContents.Should().Be(commit2ChangedFileContents);
    }

    [Fact]
    public void UpdateStackUsingRebase_WhenARemoteBranchIsDeleted_ButTheTargetBranchHasAlreadyHadAdditionalCommitsMergedInto_DoesNotRebaseOntoTheParentBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var changedFilePath = Some.Name();
        var commit1ChangedFileContents = "These are the changes in the first commit";
        var commit2ChangedFileContents = "These are the changes in the first commit, with some additional changes in the second commit";

        // We have three branches in this scenario:
        //
        // sourceBranch: A commit containing the changes that were made in branch1 but with a different hash e.g. a squash merge
        // branch1: A commit containing the original changes to the file
        // branch2: A second commit that changes the file again, building on the one from branch1
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b
                .WithName(sourceBranch)
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch1)
                .FromSourceBranch(sourceBranch)
                .WithCommit(c => c.WithChanges(changedFilePath, commit1ChangedFileContents))
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch2)
                .FromSourceBranch(branch1)
                .WithCommit(c => c.WithChanges(changedFilePath, commit2ChangedFileContents))
                .PushToRemote())
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = new GitClient(new TestLogger(testOutputHelper), repo.GitClientSettings);
        var logger = new TestLogger(testOutputHelper);
        var gitHubClient = Substitute.For<IGitHubClient>();

        // "Merge the PR" by committing the changes into the source branch and deleting the remote branch
        gitClient.ChangeBranch(sourceBranch);
        File.WriteAllText(Path.Join(repo.LocalDirectoryPath, changedFilePath), commit1ChangedFileContents);
        repo.Stage(changedFilePath);
        repo.Commit();
        repo.Push(sourceBranch);

        // Rebase the target branch onto the source branch to simulate the update after PR is merged
        repo.DeleteRemoteTrackingBranch(branch1);

        gitClient.ChangeBranch(branch2);
        gitClient.RebaseOntoNewParent(sourceBranch, branch1);

        // Make another to the source branch
        var filePath = Some.Name();
        var newContents = "Here are some new changes.";
        gitClient.ChangeBranch(sourceBranch);
        File.WriteAllText(Path.Join(repo.LocalDirectoryPath, filePath), newContents);
        repo.Stage(filePath);
        repo.Commit();
        repo.Push(sourceBranch);

        var stack = new Config.Stack("Stack1", Some.HttpsUri().ToString(), sourceBranch, [branch1, branch2]);
        var stackStatus = StackHelpers.GetStackStatus(stack, branch1, logger, gitClient, gitHubClient, false);

        // Act: Even though the parent branch (branch1) has been deleted on the remote,
        // we should not explicitly re-parent the target branch (branch2) onto the source branch
        // because we've already done that in the past and doing so can cause conflicts.
        StackHelpers.UpdateStackUsingRebase(stack, stackStatus, gitClient, inputProvider, logger);

        // Assert
        gitClient.ChangeBranch(branch2);
        var fileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, filePath));
        fileContents.Should().Be(newContents);
    }

    [Fact]
    public void UpdateStackUsingRebase_WhenARemoteBranchIsDeleted_AndLocalBranchIsDeleted_DoesNotRebaseOntoTheParentBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var changedFile1Path = Some.Name();
        var changedFile2Path = Some.Name();
        var changedFile1Contents = "Here are some changes.";
        var changedFile2Contents = "Here are some different changes.";

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(sourceBranch))
            .WithBranch(b => b.WithName(branch1).FromSourceBranch(sourceBranch))
            .WithBranch(b => b.WithName(branch2).FromSourceBranch(branch1))
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = new GitClient(new TestLogger(testOutputHelper), repo.GitClientSettings);
        var logger = new TestLogger(testOutputHelper);
        var gitHubClient = Substitute.For<IGitHubClient>();

        gitClient.ChangeBranch(sourceBranch);
        File.WriteAllText(Path.Join(repo.LocalDirectoryPath, changedFile1Path), changedFile1Contents);
        repo.Stage(changedFile1Path);
        repo.Commit();

        repo.DeleteLocalBranch(branch1);

        gitClient.ChangeBranch(branch2);
        File.WriteAllText(Path.Join(repo.LocalDirectoryPath, changedFile2Path), changedFile2Contents);
        repo.Stage(changedFile2Path);
        repo.Commit();

        var stack = new Config.Stack("Stack1", Some.HttpsUri().ToString(), sourceBranch, [branch1, branch2]);
        var stackStatus = StackHelpers.GetStackStatus(stack, branch1, logger, gitClient, gitHubClient, false);

        gitClient.Fetch(true);

        // Act
        StackHelpers.UpdateStackUsingRebase(stack, stackStatus, gitClient, inputProvider, logger);

        // Assert
        gitClient.ChangeBranch(branch2);
        var fileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, changedFile2Path));
        fileContents.Should().Be(changedFile2Contents);
    }
}