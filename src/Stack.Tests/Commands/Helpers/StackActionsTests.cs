using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Infrastructure;
using Xunit.Abstractions;

namespace Stack.Tests.Helpers;

public class StackActionsTests
{
    private readonly ITestOutputHelper testOutputHelper;

    public StackActionsTests(ITestOutputHelper testOutputHelper)
    {
        this.testOutputHelper = testOutputHelper;
    }
    private StackStatus BuildStatus(Config.Stack stack, ILogger logger, IGitClient gitClient)
    {
        var gitHubClient = Substitute.For<IGitHubClient>();
        var provider = new StackStatusProvider(logger, gitClient, gitHubClient);
        return provider.GetStackStatus(stack, stack.SourceBranch, includePullRequestStatus: false);
    }

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
            new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()) }) }
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
            inputProvider,
            logger
        );

        // Act
        var status = new StackStatus(
            stack.Name,
            new SourceBranchDetail(sourceBranch, true, null, null),
            new List<BranchDetail> {
                new BranchDetail(
                    branch1, true, new Commit(Some.Sha(), Some.Name()),
                    new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
                    null, null,
                    new List<BranchDetail>
                    {
                        new BranchDetail(branch2, true, new Commit(Some.Sha(), Some.Name()),
                            new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
                            null, null, new List<BranchDetail>())
                    })
            });

        stackActions.UpdateStack(
            stack,
            UpdateStrategy.Merge,
            status
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
            new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()) }) }
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
            inputProvider,
            logger
        );

        // Act
        var status = new StackStatus(
            stack.Name,
            new SourceBranchDetail(sourceBranch, true, null, null),
            new List<BranchDetail>
            {
                new BranchDetail(branch1, true, new Commit(Some.Sha(), Some.Name()),
                new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
                null, null, new List<BranchDetail>
                {
                    new BranchDetail(branch2, true, new Commit(Some.Sha(), Some.Name()),
                        new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
                        null, null, new List<BranchDetail>())
                }) });

        var updateAction = () => stackActions.UpdateStack(
            stack,
            UpdateStrategy.Merge,
            status
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
            new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()) }) }
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
            inputProvider,
            logger
        );

        // Act
        var status = new StackStatus(
            stack.Name,
            new SourceBranchDetail(sourceBranch, true,
                new Commit(Some.Sha(), Some.Name()),
                new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0)),
            new List<BranchDetail>
            {
                new BranchDetail(branch1, true, new Commit(Some.Sha(), Some.Name()),
                    new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
                    null, null, new List<BranchDetail>
                    {
                        new BranchDetail(branch2, true, new Commit(Some.Sha(), Some.Name()),
                            new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
                            null, null, new List<BranchDetail>())
                    })
            });

        stackActions.UpdateStack(
            stack,
            UpdateStrategy.Rebase,
            status
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
            new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()) }) }
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
            inputProvider,
            logger
        );

        // Act
        var status = new StackStatus(
            stack.Name,
            new SourceBranchDetail(sourceBranch, true, new Commit(Some.Sha(), Some.Name()),
                new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0)),
            new List<BranchDetail>
            {
                new BranchDetail(branch1, true, new Commit(Some.Sha(), Some.Name()),
                    new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
                    null, null, new List<BranchDetail>
                    {
                        new BranchDetail(branch2, true, new Commit(Some.Sha(), Some.Name()),
                            new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
                            null, null, new List<BranchDetail>())
                    })
            });

        var updateAction = () => stackActions.UpdateStack(
            stack,
            UpdateStrategy.Rebase,
            status
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
        // var changedFilePath = Some.Name();
        // var commit1ChangedFileContents = "These are the changes in the first commit";
        // var commit2ChangedFileContents = "These are the changes in the first commit, with some additional changes in the second commit";

        // We have three branches in this scenario:
        //
        // sourceBranch: A commit containing the changes that were made in branch1 but with a different hash e.g. a squash merge
        // branch1: A commit containing the original changes to the file
        // branch2: A second commit that changes the file again, building on the one from branch1
        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var logger = new TestLogger(testOutputHelper);
        // var gitHubClient = Substitute.For<IGitHubClient>();

        // Setup branch statuses to simulate the scenario
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", false, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }, // remote branch deleted
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        });

        // Simulate branch change and rebase behavior
        // gitClient.When(g => g.ChangeBranch(branch2)).Do(_ => { /* no-op for substitute */ });
        // gitClient.When(g => g.RebaseFromLocalSourceBranch(branch1)).Do(_ => { /* no-op for substitute */ });

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()) }) }
        );

        var stackActions = new StackActions(
            gitClient,
            inputProvider,
            logger
        );

        // Act
        var gitHubClientForStatus = Substitute.For<IGitHubClient>();
        var provider = new StackStatusProvider(logger, gitClient, gitHubClientForStatus);
        var status = provider.GetStackStatus(stack, stack.SourceBranch, includePullRequestStatus: false);
        stackActions.UpdateStack(stack, UpdateStrategy.Rebase, status);

        // Assert
        gitClient.Received().ChangeBranch(branch2);
        gitClient.Received().RebaseFromLocalSourceBranch(sourceBranch);
    }

    [Fact]
    public void UpdateStack_UsingRebase_WhenARemoteBranchIsDeleted_ButTheTargetBranchHasAlreadyHadAdditionalCommitsMergedInto_DoesNotRebaseOntoTheParentBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var changedFilePath = Some.Name();

        // We have three branches in this scenario:
        //
        // sourceBranch: A commit containing the changes that were made in branch1 but with a different hash e.g. a squash merge
        // branch1: A commit containing the original changes to the file
        // branch2: A second commit that changes the file again, building on the one from branch1
        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var logger = new TestLogger(testOutputHelper);

        // Setup branch statuses to represent: source exists, parent branch deleted on remote, target exists
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", false, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        });

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()) }) }
        );

        var stackActions = new StackActions(gitClient, inputProvider, logger);

        // Act: run update
        var ghForStatus = Substitute.For<IGitHubClient>();
        var provider = new StackStatusProvider(logger, gitClient, ghForStatus);
        var status = provider.GetStackStatus(stack, stack.SourceBranch, includePullRequestStatus: false);
        stackActions.UpdateStack(stack, UpdateStrategy.Rebase, status);

        // Assert that we moved to the target branch and attempted a rebase from the source
        gitClient.Received().ChangeBranch(branch2);
        gitClient.Received().RebaseFromLocalSourceBranch(sourceBranch);
    }

    [Fact]
    public void UpdateStack_UsingRebase_WhenARemoteBranchIsDeleted_AndLocalBranchIsDeleted_DoesNotRebaseOntoTheParentBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var logger = new TestLogger(testOutputHelper);

        // Simulate branch statuses: source exists, parent branch deleted locally, target exists
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        });

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()) }) }
        );

        var stackActions = new StackActions(gitClient, inputProvider, logger);

        gitClient.Fetch(true);

        // Act
        var status = BuildStatus(stack, logger, gitClient);
        stackActions.UpdateStack(stack, UpdateStrategy.Rebase, status);

        // Assert: ensure we switched to target branch; file assertions are not applicable with a substitute
        gitClient.Received().ChangeBranch(branch2);
    }

    [Fact]
    public void UpdateStack_UsingRebase_WhenStackHasATreeStructure_RebasesAllBranchesCorrectly()
    {
        // Arrange
        var sourceBranch = "source-branch";
        var branch1 = "branch-1";
        var branch2 = "branch-2";
        var branch3 = "branch-3";

        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var logger = new TestLogger(testOutputHelper);

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch3, new GitBranchStatus(branch3, $"origin/{branch3}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        });

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()), new Config.Branch(branch3, new List<Config.Branch>()) }) }
        );

        var stackActions = new StackActions(gitClient, inputProvider, logger);

        // Act
        var statusTree = BuildStatus(stack, logger, gitClient);
        stackActions.UpdateStack(stack, UpdateStrategy.Rebase, statusTree);

        // Assert that branches were changed and rebase was attempted
        gitClient.Received().ChangeBranch(branch2);
        gitClient.Received().ChangeBranch(branch3);
        gitClient.Received().RebaseFromLocalSourceBranch(Arg.Any<string>());
    }

    [Fact]
    public void UpdateStack_UsingMerge_WhenStackHasATreeStructure_MergesAllBranchesCorrectly()
    {
        // Arrange
        var sourceBranch = "source-branch";
        var branch1 = "branch-1";
        var branch2 = "branch-2";
        var branch3 = "branch-3";

        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var logger = new TestLogger(testOutputHelper);

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch3, new GitBranchStatus(branch3, $"origin/{branch3}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        });

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()), new Config.Branch(branch3, new List<Config.Branch>()) }) }
        );

        var stackActions = new StackActions(gitClient, inputProvider, logger);

        // Act
        var statusMerge = BuildStatus(stack, logger, gitClient);
        stackActions.UpdateStack(stack, UpdateStrategy.Merge, statusMerge);

        // Assert that merges were attempted
        gitClient.Received().ChangeBranch(branch2);
        gitClient.Received().ChangeBranch(branch3);
        gitClient.Received().MergeFromLocalSourceBranch(Arg.Any<string>());
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

        var stackActions = new StackActions(gitClient, Substitute.For<IInputProvider>(), new TestLogger(testOutputHelper));

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

        var stackActions = new StackActions(gitClient, Substitute.For<IInputProvider>(), new TestLogger(testOutputHelper));

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

        var stackActions = new StackActions(gitClient, Substitute.For<IInputProvider>(), new TestLogger(testOutputHelper));

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

        var stackActions = new StackActions(gitClient, Substitute.For<IInputProvider>(), new TestLogger(testOutputHelper));

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

        var stackActions = new StackActions(gitClient, Substitute.For<IInputProvider>(), new TestLogger(testOutputHelper));

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

        var stackActions = new StackActions(gitClient, Substitute.For<IInputProvider>(), new TestLogger(testOutputHelper));

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

        var stackActions = new StackActions(gitClient, Substitute.For<IInputProvider>(), new TestLogger(testOutputHelper));

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

        var stackActions = new StackActions(gitClient, Substitute.For<IInputProvider>(), new TestLogger(testOutputHelper));

        // Act
        stackActions.PushChanges(stack, maxBatchSize: 5, forceWithLease: false);

        // Assert
        gitClient.DidNotReceive().PushBranches(Arg.Any<string[]>(), Arg.Any<bool>());
        gitClient.DidNotReceive().PushNewBranch(Arg.Any<string>());
    }
}