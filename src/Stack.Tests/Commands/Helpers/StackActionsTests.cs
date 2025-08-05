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
    public void UpdateStack_UsingMerge_WhenThereAreConflictsMergingBranches_AndUpdateIsContinued_TheUpdateCompletesSuccessfully()
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
            [new Config.Branch(branch1, [new Config.Branch(branch2, [])])]
        );

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
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

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            inputProvider,
            logger
        );

        // Act
        stackActions.UpdateStack(
            stack,
            UpdateStrategy.Merge
        );

        // Assert
        gitClient.Received().ChangeBranch(branch2);
        gitClient.Received().MergeFromLocalSourceBranch(branch1);
    }

    [Fact]
    public void UpdateStack_UsingMerge_WhenThereAreConflictsMergingBranches_AndUpdateIsAborted_AnExceptionIsThrown()
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
            [new Config.Branch(branch1, [new Config.Branch(branch2, [])])]
        );
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
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

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            inputProvider,
            logger
        );

        // Act
        var updateAction = () => stackActions.UpdateStack(
            stack,
            UpdateStrategy.Merge
        );

        // Assert
        updateAction.Should().Throw<Exception>().WithMessage("Merge aborted due to conflicts.");
        gitClient.Received().AbortMerge();
        gitClient.DidNotReceive().ChangeBranch(branch2);
        gitClient.DidNotReceive().MergeFromLocalSourceBranch(branch1);
    }

    [Fact]
    public void UpdateStack_UsingRebase_WhenThereAreConflictsMergingBranches_AndUpdateIsContinued_TheUpdateCompletesSuccessfully()
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
            [new Config.Branch(branch1, [new Config.Branch(branch2, [])])]
        );

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        });

        inputProvider
            .Select(
                Questions.ContinueOrAbortRebase,
                Arg.Any<MergeConflictAction[]>(),
                Arg.Any<Func<MergeConflictAction, string>>())
            .Returns(MergeConflictAction.Continue);

        gitClient
            .When(g => g.RebaseFromLocalSourceBranch(sourceBranch))
            .Throws(new ConflictException());

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            inputProvider,
            logger
        );

        // Act
        stackActions.UpdateStack(
            stack,
            UpdateStrategy.Rebase
        );

        // Assert
        gitClient.Received().ChangeBranch(branch2);
        gitClient.Received().RebaseFromLocalSourceBranch(sourceBranch);
    }

    [Fact]
    public void UpdateStack_UsingRebase_WhenThereAreConflictsMergingBranches_AndUpdateIsAborted_AnExceptionIsThrown()
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
            [new Config.Branch(branch1, [new Config.Branch(branch2, [])])]
        );
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        });

        gitClient
            .When(g => g.RebaseFromLocalSourceBranch(sourceBranch))
            .Throws(new ConflictException());

        inputProvider
            .Select(
                Questions.ContinueOrAbortRebase,
                Arg.Any<MergeConflictAction[]>(),
                Arg.Any<Func<MergeConflictAction, string>>())
            .Returns(MergeConflictAction.Abort);

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            inputProvider,
            logger
        );

        // Act
        var updateAction = () => stackActions.UpdateStack(
            stack,
            UpdateStrategy.Rebase
        );

        // Assert
        updateAction.Should().Throw<Exception>().WithMessage("Rebase aborted due to conflicts.");
        gitClient.Received().AbortRebase();
    }

    [Fact]
    public void UpdateStack_UsingRebase_WhenARemoteBranchIsDeleted_RebasesOntoTheParentBranchToAvoidConflicts()
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

        var stackActions = new StackActions(
            gitClient,
            gitHubClient,
            inputProvider,
            logger
        );

        // Act
        stackActions.UpdateStack(stack, UpdateStrategy.Rebase);

        // Assert
        gitClient.ChangeBranch(branch2);
        var fileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, changedFilePath));
        fileContents.Should().Be(commit2ChangedFileContents);
    }

    [Fact]
    public void UpdateStack_UsingRebase_WhenARemoteBranchIsDeleted_ButTheTargetBranchHasAlreadyHadAdditionalCommitsMergedInto_DoesNotRebaseOntoTheParentBranch()
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

        var stackActions = new StackActions(
            gitClient,
            gitHubClient,
            inputProvider,
            logger
        );

        // Act: Even though the parent branch (branch1) has been deleted on the remote,
        // we should not explicitly re-parent the target branch (branch2) onto the source branch
        // because we've already done that in the past and doing so can cause conflicts.
        stackActions.UpdateStack(stack, UpdateStrategy.Rebase);

        // Assert
        gitClient.ChangeBranch(branch2);
        var fileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, filePath));
        fileContents.Should().Be(newContents);
    }

    [Fact]
    public void UpdateStack_UsingRebase_WhenARemoteBranchIsDeleted_AndLocalBranchIsDeleted_DoesNotRebaseOntoTheParentBranch()
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

        var stackActions = new StackActions(
            gitClient,
            gitHubClient,
            inputProvider,
            logger
        );

        gitClient.Fetch(true);

        // Act
        stackActions.UpdateStack(stack, UpdateStrategy.Rebase);

        // Assert
        gitClient.ChangeBranch(branch2);
        var fileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, changedFile2Path));
        fileContents.Should().Be(changedFile2Contents);
    }

    [Fact]
    public void UpdateStack_UsingRebase_WhenStackHasATreeStructure_RebasesAllBranchesCorrectly()
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

        var stackActions = new StackActions(
            gitClient,
            gitHubClient,
            inputProvider,
            logger
        );

        // Act
        stackActions.UpdateStack(stack, UpdateStrategy.Rebase);

        // Assert
        gitClient.ChangeBranch(branch2);
        var branch2FileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, changedFilePath));
        branch2FileContents.Should().Be(commit1ChangedFileContents);

        gitClient.ChangeBranch(branch3);
        var branch3FileContents = File.ReadAllText(Path.Join(repo.LocalDirectoryPath, changedFilePath));
        branch3FileContents.Should().Be(commit1ChangedFileContents);
    }

    [Fact]
    public void UpdateStack_UsingMerge_WhenStackHasATreeStructure_MergesAllBranchesCorrectly()
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

        var stackActions = new StackActions(
            gitClient,
            gitHubClient,
            inputProvider,
            logger
        );

        // Act
        stackActions.UpdateStack(stack, UpdateStrategy.Merge);

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
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branchAheadOfRemote, new GitBranchStatus(branchAheadOfRemote, $"origin/{branchAheadOfRemote}", true, false, 3, 0, new Commit(Some.Sha(), Some.Name())) },
            { branchNotAheadOfRemote, new GitBranchStatus(branchNotAheadOfRemote, $"origin/{branchNotAheadOfRemote}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
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

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            Substitute.For<IInputProvider>(),
            new TestLogger(testOutputHelper)
        );

        // Act
        stackActions.PushChanges(stack, 5, false);

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

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            Substitute.For<IInputProvider>(),
            new TestLogger(testOutputHelper)
        );

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

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            Substitute.For<IInputProvider>(),
            new TestLogger(testOutputHelper)
        );

        // Act
        stackActions.PullChanges(stack);

        // Assert
        gitClient.DidNotReceive().PullBranch(sourceBranch);
        gitClient.Received().PullBranch(branchThatExistsInRemote);
        gitClient.DidNotReceive().PullBranch(branchThatDoesNotExistInRemote);
    }

    [Fact]
    public void PushChanges_WhenSomeBranchesDoNotExistInRemote_OnlyPushesBranchesThatExistInRemote()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchThatExistsInRemoteAndIsAhead = Some.BranchName();
        var branchThatDoesNotExistInRemoteButIsAhead = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();

        var branchStatus = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branchThatExistsInRemoteAndIsAhead, new GitBranchStatus(branchThatExistsInRemoteAndIsAhead, $"origin/{branchThatExistsInRemoteAndIsAhead}", true, false, 2, 0, new Commit(Some.Sha(), Some.Name())) },
            { branchThatDoesNotExistInRemoteButIsAhead, new GitBranchStatus(branchThatDoesNotExistInRemoteButIsAhead, $"origin/{branchThatDoesNotExistInRemoteButIsAhead}", false, false, 2, 0, new Commit(Some.Sha(), Some.Name())) }
        };

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatus);

        var branchesPushedToRemote = new List<string>();

        gitClient
            .WhenForAnyArgs(g => g.PushBranches(Arg.Any<string[]>(), Arg.Any<bool>()))
            .Do((c) => branchesPushedToRemote.AddRange(c.Arg<string[]>()));

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchThatExistsInRemoteAndIsAhead))
            .WithBranch(b => b.WithName(branchThatDoesNotExistInRemoteButIsAhead))
            .Build();

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            Substitute.For<IInputProvider>(),
            new TestLogger(testOutputHelper)
        );

        // Act
        stackActions.PushChanges(stack, 5, false);

        // Assert
        branchesPushedToRemote.ToArray().Should().BeEquivalentTo([branchThatExistsInRemoteAndIsAhead]);
    }

    [Fact]
    public void PushChanges_WhenSomeBranchesHaveNoRemoteTrackingBranch_PushesThemAsNewBranches()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var existingBranchAhead = Some.BranchName();
        var newBranchWithNoRemote = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();

        var branchStatus = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { existingBranchAhead, new GitBranchStatus(existingBranchAhead, $"origin/{existingBranchAhead}", true, false, 2, 0, new Commit(Some.Sha(), Some.Name())) },
            { newBranchWithNoRemote, new GitBranchStatus(newBranchWithNoRemote, null, false, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        };

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatus);

        var branchesPushedToRemote = new List<string>();
        var newBranchesPushed = new List<string>();

        gitClient
            .WhenForAnyArgs(g => g.PushBranches(Arg.Any<string[]>(), Arg.Any<bool>()))
            .Do((c) => branchesPushedToRemote.AddRange(c.Arg<string[]>()));

        gitClient
            .WhenForAnyArgs(g => g.PushNewBranch(Arg.Any<string>()))
            .Do((c) => newBranchesPushed.Add(c.Arg<string>()));

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(existingBranchAhead))
            .WithBranch(b => b.WithName(newBranchWithNoRemote))
            .Build();

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            Substitute.For<IInputProvider>(),
            new TestLogger(testOutputHelper)
        );

        // Act
        stackActions.PushChanges(stack, 5, false);

        // Assert
        newBranchesPushed.Should().BeEquivalentTo([newBranchWithNoRemote]);
        branchesPushedToRemote.Should().BeEquivalentTo([existingBranchAhead]);
    }

    [Fact]
    public void PushChanges_WhenMaxBatchSizeIsSmaller_PushesBranchesInMultipleBatches()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var branch3 = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();

        var branchStatus = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 1, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 2, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch3, new GitBranchStatus(branch3, $"origin/{branch3}", true, false, 1, 0, new Commit(Some.Sha(), Some.Name())) }
        };

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatus);

        var pushCalls = new List<List<string>>();

        gitClient
            .WhenForAnyArgs(g => g.PushBranches(Arg.Any<string[]>(), Arg.Any<bool>()))
            .Do((c) => pushCalls.Add(c.Arg<string[]>().ToList()));

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branch1))
            .WithBranch(b => b.WithName(branch2))
            .WithBranch(b => b.WithName(branch3))
            .Build();

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            Substitute.For<IInputProvider>(),
            new TestLogger(testOutputHelper)
        );

        // Act
        stackActions.PushChanges(stack, maxBatchSize: 2, forceWithLease: false);

        // Assert
        pushCalls.Should().HaveCount(2);
        pushCalls[0].Should().HaveCount(2);
        pushCalls[1].Should().HaveCount(1);
        pushCalls.SelectMany(batch => batch).Should().BeEquivalentTo([branch1, branch2, branch3]);
    }

    [Fact]
    public void PushChanges_WhenForceWithLeaseIsTrue_PassesForceWithLeaseParameterToPushBranches()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchAhead = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();

        var branchStatus = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branchAhead, new GitBranchStatus(branchAhead, $"origin/{branchAhead}", true, false, 1, 0, new Commit(Some.Sha(), Some.Name())) }
        };

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatus);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchAhead))
            .Build();

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            Substitute.For<IInputProvider>(),
            new TestLogger(testOutputHelper)
        );

        // Act
        stackActions.PushChanges(stack, maxBatchSize: 5, forceWithLease: true);

        // Assert
        gitClient.Received().PushBranches(Arg.Is<string[]>(branches => branches.Contains(branchAhead)), true);
    }

    [Fact]
    public void PushChanges_WhenNoBranchesNeedToBePushed_DoesNotCallPushMethods()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchUpToDate = Some.BranchName();
        var branchBehind = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();

        var branchStatus = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branchUpToDate, new GitBranchStatus(branchUpToDate, $"origin/{branchUpToDate}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branchBehind, new GitBranchStatus(branchBehind, $"origin/{branchBehind}", true, false, 0, 2, new Commit(Some.Sha(), Some.Name())) }
        };

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatus);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchUpToDate))
            .WithBranch(b => b.WithName(branchBehind))
            .Build();

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            Substitute.For<IInputProvider>(),
            new TestLogger(testOutputHelper)
        );

        // Act
        stackActions.PushChanges(stack, maxBatchSize: 5, forceWithLease: false);

        // Assert
        gitClient.DidNotReceive().PushBranches(Arg.Any<string[]>(), Arg.Any<bool>());
        gitClient.DidNotReceive().PushNewBranch(Arg.Any<string>());
    }

    [Fact]
    public void PushChanges_WithRealGitRepository_PushesOnlyBranchesThatAreAheadAndExistInRemote()
    {
        // Arrange
        var sourceBranch = "main";
        var branch1 = "feature-1";
        var branch2 = "feature-2";
        var branch3 = "feature-3";

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b
                .WithName(sourceBranch)
                .WithNumberOfEmptyCommits(2)
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch1)
                .FromSourceBranch(sourceBranch)
                .WithNumberOfEmptyCommits(1)
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch2)
                .FromSourceBranch(branch1)
                .WithNumberOfEmptyCommits(1)
                .PushToRemote())
            .WithBranch(b => b
                .WithName(branch3)
                .FromSourceBranch(branch2)
                .WithNumberOfEmptyCommits(1)
                .PushToRemote())
            .Build();

        var gitClient = new GitClient(new TestLogger(testOutputHelper), repo.GitClientSettings);
        var logger = new TestLogger(testOutputHelper);

        // Make some local changes to branch1 and branch2 (but not branch3)
        gitClient.ChangeBranch(branch1);
        File.WriteAllText(Path.Join(repo.LocalDirectoryPath, "local-change1.txt"), "local content 1");
        repo.Stage("local-change1.txt");
        repo.Commit("Local change to branch1");

        gitClient.ChangeBranch(branch2);
        File.WriteAllText(Path.Join(repo.LocalDirectoryPath, "local-change2.txt"), "local content 2");
        repo.Stage("local-change2.txt");
        repo.Commit("Local change to branch2");

        // Delete the remote tracking branch for branch3
        repo.DeleteRemoteTrackingBranch(branch3);

        var stack = new Config.Stack(
            "TestStack",
            repo.RemoteUri,
            sourceBranch,
            [
                new Config.Branch(branch1, [
                    new Config.Branch(branch2, [
                        new Config.Branch(branch3, [])
                    ])
                ])
            ]
        );

        var stackActions = new StackActions(
            gitClient,
            Substitute.For<IGitHubClient>(),
            Substitute.For<IInputProvider>(),
            logger
        );

        // Get initial commit counts to verify pushes
        var initialBranch1RemoteCommitCount = repo.GetCommitsReachableFromRemoteBranch(branch1).Count;
        var initialBranch2RemoteCommitCount = repo.GetCommitsReachableFromRemoteBranch(branch2).Count;

        // Act
        stackActions.PushChanges(stack, maxBatchSize: 5, forceWithLease: false);

        // Assert
        // Branch1 and branch2 should have been pushed (they had local changes and exist in remote)
        var finalBranch1RemoteCommitCount = repo.GetCommitsReachableFromRemoteBranch(branch1).Count;
        var finalBranch2RemoteCommitCount = repo.GetCommitsReachableFromRemoteBranch(branch2).Count;

        finalBranch1RemoteCommitCount.Should().Be(initialBranch1RemoteCommitCount + 1, "branch1 should have been pushed");
        finalBranch2RemoteCommitCount.Should().Be(initialBranch2RemoteCommitCount + 1, "branch2 should have been pushed");

        // Verify the pushed content exists in remote by checking the tip commit messages
        var branch1RemoteTip = repo.GetTipOfRemoteBranch(branch1);
        var branch2RemoteTip = repo.GetTipOfRemoteBranch(branch2);

        branch1RemoteTip?.Message.Should().Contain("Local change to branch1", "branch1 local changes should be in remote");
        branch2RemoteTip?.Message.Should().Contain("Local change to branch2", "branch2 local changes should be in remote");

        repo.GetTipOfRemoteBranch(branch3).Should().BeNull("branch3 was deleted on remote, and should not have been pushed");
    }
}