using FluentAssertions;
using NSubstitute;
using Stack.Commands;
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

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            [new Config.Branch(branch1, []), new Config.Branch(branch2, [])]
        );
        var branchDetail1 = new BranchDetail(
            branch1, true, new Commit(Some.Sha(), Some.Name()),
            new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0), null, null,
            [new BranchDetail(
                branch2, true, new Commit(Some.Sha(), Some.Name()),
                new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0), null, null, [])]);
        var stackStatus = new StackStatus("Stack1", new SourceBranchDetail(sourceBranch, true, null, null), [branchDetail1]);

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

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            [new Config.Branch(branch1, []), new Config.Branch(branch2, [])]
        );
        var branchDetail1 = new BranchDetail(branch1, true, new Commit(Some.Sha(), Some.Name()), new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0), null, null, []);
        var branchDetail2 = new BranchDetail(branch2, true, new Commit(Some.Sha(), Some.Name()), new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0), null, null, []);
        var stackStatus = new StackStatus("Stack1", new SourceBranchDetail(sourceBranch, true, null, null), [branchDetail1, branchDetail2]);

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

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            [new Config.Branch(branch1, [new Config.Branch(branch2, [])])]
        );
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

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            [new Config.Branch(branch1, [new Config.Branch(branch2, [])])]
        );
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

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            [new Config.Branch(branch1, [new Config.Branch(branch2, [])])]
        );
        var stackStatus = StackHelpers.GetStackStatus(stack, branch1, logger, gitClient, gitHubClient, false);

        gitClient.Fetch(true);

        // Act
        StackHelpers.UpdateStackUsingRebase(stack, stackStatus, gitClient, inputProvider, logger);

        // Assert
        gitClient.ChangeBranch(branch2);
        var fileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, changedFile2Path));
        fileContents.Should().Be(changedFile2Contents);
    }

    [Fact]
    public void UpdateStackUsingRebase_WhenStackHasATreeStructure_RebasesAllBranchesCorrectly()
    {
        // Arrange
        var sourceBranch = "source-branch";
        var branch1 = "branch-1";
        var branch2 = "branch-2";
        var branch3 = "branch-3";
        var changedFilePath = "change-file-1";
        var commit1ChangedFileContents = "These are the changes in the first commit";

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b
                .WithName(sourceBranch)
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch1)
                .FromSourceBranch(sourceBranch)
                .WithCommit(c => c.WithChanges("file-1", "file-1-changes"))
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch2)
                .FromSourceBranch(branch1)
                .WithCommit(c => c.WithChanges("file-2", "file-2-changes"))
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch3)
                .FromSourceBranch(branch1)
                .WithCommit(c => c.WithChanges("file-3", "file-3-changes"))
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

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            [new Config.Branch(branch1, [new Config.Branch(branch2, []), new Config.Branch(branch3, [])])]
        );
        var stackStatus = StackHelpers.GetStackStatus(stack, sourceBranch, logger, gitClient, gitHubClient, false);

        // Act
        StackHelpers.UpdateStackUsingRebase(stack, stackStatus, gitClient, inputProvider, logger);

        // Assert
        gitClient.ChangeBranch(branch2);
        var branch2FileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, changedFilePath));
        branch2FileContents.Should().Be(commit1ChangedFileContents);

        gitClient.ChangeBranch(branch3);
        var branch3FileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, changedFilePath));
        branch3FileContents.Should().Be(commit1ChangedFileContents);
    }

    [Fact]
    public void UpdateStackUsingMerge_WhenStackHasATreeStructure_MergesAllBranchesCorrectly()
    {
        // Arrange
        var sourceBranch = "source-branch";
        var branch1 = "branch-1";
        var branch2 = "branch-2";
        var branch3 = "branch-3";
        var changedFilePath = "change-file-1";
        var commit1ChangedFileContents = "These are the changes in the first commit";

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b
                .WithName(sourceBranch)
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch1)
                .FromSourceBranch(sourceBranch)
                .WithCommit(c => c.WithChanges("file-1", "file-1-changes"))
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch2)
                .FromSourceBranch(branch1)
                .WithCommit(c => c.WithChanges("file-2", "file-2-changes"))
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch3)
                .FromSourceBranch(branch1)
                .WithCommit(c => c.WithChanges("file-3", "file-3-changes"))
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

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            [new Config.Branch(branch1, [new Config.Branch(branch2, []), new Config.Branch(branch3, [])])]
        );
        var stackStatus = StackHelpers.GetStackStatus(stack, sourceBranch, logger, gitClient, gitHubClient, false);

        // Act
        StackHelpers.UpdateStackUsingMerge(stack, stackStatus, gitClient, inputProvider, logger);

        // Assert
        gitClient.ChangeBranch(branch2);
        var branch2FileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, changedFilePath));
        branch2FileContents.Should().Be(commit1ChangedFileContents);

        gitClient.ChangeBranch(branch3);
        var branch3FileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, changedFilePath));
        branch3FileContents.Should().Be(commit1ChangedFileContents);
    }

    [Fact]
    public void PushChanges_WhenSomeLocalBranchesAreAhead_OnlyPushesChangesForBranchesThatAreAhead()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchAheadOfRemote = Some.BranchName();
        var branchNotAheadOfRemote = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();

        var branchStatus = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name()), null) },
            { branchAheadOfRemote, new GitBranchStatus(branchAheadOfRemote, $"origin/{branchAheadOfRemote}", true, false, 3, 0, new Commit(Some.Sha(), Some.Name()), null) },
            { branchNotAheadOfRemote, new GitBranchStatus(branchNotAheadOfRemote, $"origin/{branchNotAheadOfRemote}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name()), null) }
        };

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatus);

        var branchesPushedToRemote = new List<string>();

        gitClient
            .WhenForAnyArgs(g => g.PushBranches(Arg.Any<string[]>(), Arg.Any<bool>()))
            .Do((c) => branchesPushedToRemote.AddRange(c.Arg<string[]>()));

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchAheadOfRemote))
            .WithBranch(b => b.WithName(branchNotAheadOfRemote))
            .Build();

        // Act
        StackHelpers.PushChanges(stack, 5, false, gitClient, new TestLogger(testOutputHelper));

        // Assert
        branchesPushedToRemote.ToArray().Should().BeEquivalentTo([branchAheadOfRemote]);
    }

    [Fact]
    public void PullChanges_WhenSomeBranchesHaveChanges_AndOthersDoNot_OnlyPullsChangesForBranchesThatNeedIt()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchWithRemoteChanges = Some.BranchName();
        var branchWithoutRemoteChanges = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();
        // Simulate that the branch with remote changes is the current branch so it is pulled
        gitClient.GetCurrentBranch().Returns(branchWithRemoteChanges);

        var branchStatus = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name()), null) },
            { branchWithRemoteChanges, new GitBranchStatus(branchWithRemoteChanges, $"origin/{branchWithRemoteChanges}", true, false, 0, 3, new Commit(Some.Sha(), Some.Name()), null) },
            { branchWithoutRemoteChanges, new GitBranchStatus(branchWithoutRemoteChanges, $"origin/{branchWithoutRemoteChanges}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name()), null) }
        };

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatus);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchWithRemoteChanges))
            .WithBranch(b => b.WithName(branchWithoutRemoteChanges))
            .Build();

        // Act
        StackHelpers.PullChanges(stack, gitClient, new TestLogger(testOutputHelper));

        // Assert
        gitClient.Received(1).PullBranch(branchWithRemoteChanges);
        gitClient.DidNotReceive().PullBranch(sourceBranch);
        gitClient.DidNotReceive().PullBranch(branchWithoutRemoteChanges);
        gitClient.DidNotReceive().FetchBranchRefSpecs(Arg.Any<string[]>()); // only current branch updated, no fetch required
    }
    [Fact]
    public void PullChanges_WhenOnlyNonCurrentBranchesBehind_FetchesThem()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var statuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 0, new Commit(Some.Sha(), Some.Name()), null) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 2, new Commit(Some.Sha(), Some.Name()), null) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 3, new Commit(Some.Sha(), Some.Name()), null) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(statuses);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branch1))
            .WithBranch(b => b.WithName(branch2))
            .Build();

        // Act
        StackHelpers.PullChanges(stack, gitClient, new TestLogger(testOutputHelper));

        // Assert
        gitClient.DidNotReceive().PullBranch(Arg.Any<string>());
        gitClient.Received().FetchBranchRefSpecs(Arg.Is<string[]>(a => a.Length == 2 && a.Contains(branch1) && a.Contains(branch2)));
    }

    [Fact]
    public void PullChanges_WhenOnlyCurrentBranchBehind_PullsIt()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        var statuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 5, new Commit(Some.Sha(), Some.Name()), null) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(statuses);

        var stack = new TestStackBuilder().WithSourceBranch(sourceBranch).Build();

        // Act
        StackHelpers.PullChanges(stack, gitClient, new TestLogger(testOutputHelper));

        // Assert
        gitClient.Received(1).PullBranch(sourceBranch);
        gitClient.DidNotReceive().FetchBranchRefSpecs(Arg.Any<string[]>());
    }

    [Fact]
    public void PullChanges_WhenCurrentAndOtherBranchesBehind_PullsCurrentAndFetchesOthers()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var otherBranch = Some.BranchName();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        var statuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 1, new Commit(Some.Sha(), Some.Name()), null) },
            { otherBranch, new GitBranchStatus(otherBranch, $"origin/{otherBranch}", true, false, 0, 2, new Commit(Some.Sha(), Some.Name()), null) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(statuses);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(otherBranch))
            .Build();

        // Act
        StackHelpers.PullChanges(stack, gitClient, new TestLogger(testOutputHelper));

        // Assert
        gitClient.Received(1).PullBranch(sourceBranch);
        gitClient.Received(1).FetchBranchRefSpecs(Arg.Is<string[]>(a => a.Length == 1 && a[0] == otherBranch));
    }

    [Fact]
    public void PullChanges_WhenNoBranchesBehind_DoesNothing()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var otherBranch = Some.BranchName();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        var statuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 0, new Commit(Some.Sha(), Some.Name()), null) },
            { otherBranch, new GitBranchStatus(otherBranch, $"origin/{otherBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name()), null) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(statuses);
        var stack = new TestStackBuilder().WithSourceBranch(sourceBranch).WithBranch(b => b.WithName(otherBranch)).Build();

        // Act
        StackHelpers.PullChanges(stack, gitClient, new TestLogger(testOutputHelper));

        // Assert
        gitClient.DidNotReceive().PullBranch(Arg.Any<string>());
        gitClient.DidNotReceive().FetchBranchRefSpecs(Arg.Any<string[]>());
    }

    [Fact]
    public void PullChanges_WhenBranchIsBehind_AndCheckedOutInAnotherWorktree_PullsItDirectly()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchInOtherWorktree = Some.BranchName();
        var worktreePath = $"C:/temp/{Some.Name()}";

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        var statuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 0, new Commit(Some.Sha(), Some.Name()), null) },
            // Simulate branch existing in another worktree (marker '+') by providing WorktreePath
            { branchInOtherWorktree, new GitBranchStatus(branchInOtherWorktree, $"origin/{branchInOtherWorktree}", true, false, 0, 4, new Commit(Some.Sha(), Some.Name()), worktreePath) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(statuses);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchInOtherWorktree))
            .Build();

        // Act
        StackHelpers.PullChanges(stack, gitClient, new TestLogger(testOutputHelper));

        // Assert
        gitClient.Received(1).PullBranchForWorktree(branchInOtherWorktree, worktreePath);
        gitClient.DidNotReceive().FetchBranchRefSpecs(Arg.Any<string[]>());
        gitClient.DidNotReceive().PullBranch(branchInOtherWorktree);
    }
}