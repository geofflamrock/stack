using FluentAssertions;
using Meziantou.Extensions.Logging.Xunit;
using NSubstitute;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Xunit.Abstractions;

namespace Stack.Tests.Helpers;

public class StackActionsTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task UpdateStack_UsingMerge_WhenConflictAbortedBeforeProgressRecorded_ThrowsAbortException()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var feature = Some.BranchName();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stack = new Config.Stack("Stack1", Some.HttpsUri().ToString(), sourceBranch, new List<Config.Branch> { new(feature, []) });

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch,$"origin/{sourceBranch}",true,false,0,0,new Commit(Some.Sha(), Some.Name())) },
            { feature, new GitBranchStatus(feature,$"origin/{feature}",true,false,0,0,new Commit(Some.Sha(), Some.Name())) }
        });

        // Trigger conflict
        gitClient.When(g => g.MergeFromLocalSourceBranch(sourceBranch)).Throws(new ConflictException());
        // Simulate: initial check says merge in progress, then still in progress once, then not in progress with HEAD unchanged => aborted
        gitClient.IsMergeInProgress().Returns(true, true, false);

        var head = Some.Sha();
        gitClient.GetHeadSha().Returns(head, head, head); // unchanged
        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var actions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        var act = async () => await actions.UpdateStack(stack, UpdateStrategy.Merge, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Merge aborted due to conflicts.");
    }

    [Fact]
    public async Task UpdateStack_UsingMerge_WhenConflictResolved_CompletesSuccessfully()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var feature = Some.BranchName();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stack = new Config.Stack("Stack1", Some.HttpsUri().ToString(), sourceBranch, new List<Config.Branch> { new(feature, []) });

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch,$"origin/{sourceBranch}",true,false,0,0,new Commit(Some.Sha(), Some.Name())) },
            { feature, new GitBranchStatus(feature,$"origin/{feature}",true,false,0,0,new Commit(Some.Sha(), Some.Name())) }
        });

        gitClient.When(g => g.MergeFromLocalSourceBranch(sourceBranch)).Throws(new ConflictException());

        // Merge progress -> then resolved (not in progress) with different HEAD
        gitClient.IsMergeInProgress().Returns(true, false);
        gitClient.GetHeadSha().Returns(Some.Sha(), Some.Sha()); // changed
        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var actions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        await actions.UpdateStack(stack, UpdateStrategy.Merge, CancellationToken.None);

        // Assert
        gitClient.Received().ChangeBranch(feature);
    }

    [Fact]
    public async Task UpdateStack_UsingRebase_WhenConflictAbortedBeforeProgressRecorded_ThrowsAbortException()
    {
        // Arrange
        var source = Some.BranchName();
        var feature = Some.BranchName();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stack = new Config.Stack("Stack1", Some.HttpsUri().ToString(), source, new List<Config.Branch> { new(feature, []) });

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { source, new GitBranchStatus(source,$"origin/{source}",true,false,0,0,new Commit(Some.Sha(), Some.Name())) },
            { feature, new GitBranchStatus(feature,$"origin/{feature}",true,false,0,0,new Commit(Some.Sha(), Some.Name())) }
        });

        gitClient.When(g => g.RebaseFromLocalSourceBranch(source)).Throws(new ConflictException());
        gitClient.IsRebaseInProgress().Returns(true, true, false);
        var origHead = Some.Sha();

        // During rebase conflict HEAD may move; ensure orig head stored and final head equals orig to simulate abort
        gitClient.GetOriginalHeadSha().Returns(origHead);
        gitClient.GetHeadSha().Returns(origHead, origHead, origHead);
        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var actions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        var act = async () => await actions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Rebase aborted due to conflicts.");
    }

    [Fact]
    public async Task UpdateStack_UsingRebase_WhenConflictResolved_CompletesSuccessfully()
    {
        // Arrange
        var source = Some.BranchName();
        var feature = Some.BranchName();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stack = new Config.Stack("Stack1", Some.HttpsUri().ToString(), source, new List<Config.Branch> { new(feature, []) });

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { source, new GitBranchStatus(source,$"origin/{source}",true,false,0,0,new Commit(Some.Sha(), Some.Name())) },
            { feature, new GitBranchStatus(feature,$"origin/{feature}",true,false,0,0,new Commit(Some.Sha(), Some.Name())) }
        });

        gitClient.When(g => g.RebaseFromLocalSourceBranch(source)).Throws(new ConflictException());
        gitClient.IsRebaseInProgress().Returns(true, false);

        var origHead = Some.Sha();
        var newHead = Some.Sha();
        gitClient.GetOriginalHeadSha().Returns(origHead);
        gitClient.GetHeadSha().Returns(newHead, newHead); // changed from original

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var actions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        await actions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert
        gitClient.Received().ChangeBranch(feature);
    }

    // [Fact]
    // public async Task UpdateStack_UsingRebase_WhenARemoteBranchIsDeleted_RebasesOntoTheParentBranchToAvoidConflicts()
    // {
    //     // Arrange
    //     var sourceBranch = Some.BranchName();
    //     var branch1 = Some.BranchName();
    //     var branch2 = Some.BranchName();
    //     var gitClient = Substitute.For<IGitClient>();
    //     var gitHubClient = Substitute.For<IGitHubClient>();
    //     var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
    //     var console = new TestDisplayProvider(testOutputHelper);

    //     // Setup branch statuses to simulate the scenario
    //     gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
    //     {
    //         { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
    //         { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", false, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }, // remote branch deleted
    //         { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
    //     });
    //     gitClient.IsAncestor(branch2, branch1).Returns(true);

    //     var stack = new Config.Stack(
    //         "Stack1",
    //         Some.HttpsUri().ToString(),
    //         sourceBranch,
    //         new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()) }) }
    //     );

    //     var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
    //     var factory = Substitute.For<IGitClientFactory>();
    //     factory.Create(executionContext.WorkingDirectory).Returns(gitClient);
    //     var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

    //     // Act
    //     await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

    //     // Assert
    //     gitClient.Received().ChangeBranch(branch2);
    //     gitClient.Received().RebaseOntoNewParent(sourceBranch, branch1);
    // }

    // [Fact]
    // public async Task UpdateStack_UsingRebase_WhenARemoteBranchIsDeleted_ButTheTargetBranchHasAlreadyHadAdditionalCommitsMergedInto_DoesNotRebaseOntoTheParentBranch()
    // {
    //     // Arrange
    //     var sourceBranch = Some.BranchName();
    //     var branch1 = Some.BranchName();
    //     var branch2 = Some.BranchName();
    //     var changedFilePath = Some.Name();

    //     var gitClient = Substitute.For<IGitClient>();
    //     var gitHubClient = Substitute.For<IGitHubClient>();
    //     var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
    //     var console = new TestDisplayProvider(testOutputHelper);

    //     // Setup branch statuses to simulate the scenario
    //     gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
    //     {
    //         { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
    //         { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", false, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }, // remote branch deleted
    //         { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
    //     });
    //     gitClient.IsAncestor(branch2, branch1).Returns(false);

    //     var stack = new Config.Stack(
    //         "Stack1",
    //         Some.HttpsUri().ToString(),
    //         sourceBranch,
    //         new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()) }) }
    //     );

    //     var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
    //     var factory = Substitute.For<IGitClientFactory>();
    //     factory.Create(executionContext.WorkingDirectory).Returns(gitClient);
    //     var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

    //     // Act
    //     await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

    //     // Assert
    //     gitClient.Received().ChangeBranch(branch2);
    //     gitClient.Received().RebaseFromLocalSourceBranch(sourceBranch);
    // }

    [Fact]
    public async Task UpdateStack_UsingRebase_WhenARemoteBranchIsDeleted_AndLocalBranchIsDeleted_DoesNotRebaseOntoTheParentBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);

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

        var console = new TestDisplayProvider(testOutputHelper);
        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        gitClient.Fetch(true);

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert
        gitClient.Received().ChangeBranch(branch2);
    }

    [Fact]
    public async Task UpdateStack_UsingRebase_WhenStackHasATreeStructure_RebasesAllBranchesCorrectly()
    {
        // Arrange
        var sourceBranch = "source-branch";
        var branch1 = "branch-1";
        var branch2 = "branch-2";
        var branch3 = "branch-3";

        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

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

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert
        Received.InOrder(() =>
        {
            gitClient.ChangeBranch(branch2);
            gitClient.RebaseFromLocalSourceBranch(branch1);
            gitClient.ChangeBranch(branch2);
            gitClient.RebaseFromLocalSourceBranch(sourceBranch);
            gitClient.ChangeBranch(branch3);
            gitClient.RebaseFromLocalSourceBranch(branch1);
            gitClient.ChangeBranch(branch3);
            gitClient.RebaseFromLocalSourceBranch(sourceBranch);
        });
    }

    [Fact]
    public async Task UpdateStack_UsingMerge_WhenStackHasATreeStructure_MergesAllBranchesCorrectly()
    {
        // Arrange
        var sourceBranch = "source-branch";
        var branch1 = "branch-1";
        var branch2 = "branch-2";
        var branch3 = "branch-3";

        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

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

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Merge, CancellationToken.None);

        // Assert that merges were attempted
        Received.InOrder(() =>
        {
            gitClient.ChangeBranch(branch1);
            gitClient.MergeFromLocalSourceBranch(sourceBranch);
            gitClient.ChangeBranch(branch2);
            gitClient.MergeFromLocalSourceBranch(branch1);
            gitClient.ChangeBranch(branch1);
            gitClient.MergeFromLocalSourceBranch(sourceBranch);
            gitClient.ChangeBranch(branch3);
            gitClient.MergeFromLocalSourceBranch(branch1);
        });
    }

    [Fact]
    public void PullChanges_WhenSomeBranchesHaveChanges_AndOthersDoNot_OnlyPullsChangesForBranchesThatNeedIt()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchWithRemoteChanges = Some.BranchName();
        var branchWithoutRemoteChanges = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

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

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        stackActions.PullChanges(stack);

        // Assert
        gitClient.Received().FetchBranchRefSpecs(Arg.Is<string[]>(a => a.Length == 1 && a[0] == branchWithRemoteChanges));
        gitClient.DidNotReceive().FetchBranchRefSpecs(Arg.Is<string[]>(a => a.Contains(branchWithoutRemoteChanges) || a.Contains(sourceBranch)));
    }

    [Fact]
    public void PullChanges_WhenSomeBranchesDoNotExistInRemote_OnlyPullsBranchesThatExistInRemote()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchThatExistsInRemote = Some.BranchName();
        var branchThatDoesNotExistInRemote = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

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

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        stackActions.PullChanges(stack);

        // Assert
        gitClient.Received().FetchBranchRefSpecs(Arg.Is<string[]>(a => a.Length == 1 && a[0] == branchThatExistsInRemote));
        gitClient.DidNotReceive().FetchBranchRefSpecs(Arg.Is<string[]>(a => a.Contains(branchThatDoesNotExistInRemote) || a.Contains(sourceBranch)));
    }

    [Fact]
    public void PullChanges_WhenOnlyNonCurrentBranchesBehind_FetchesThem()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

        var statuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 2, new Commit(Some.Sha(), Some.Name())) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 3, new Commit(Some.Sha(), Some.Name())) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(statuses);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branch1))
            .WithBranch(b => b.WithName(branch2))
            .Build();

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        var worktreeGitClient = Substitute.For<IGitClient>();
        var worktreePath = "/worktree";
        factory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        factory.Create(worktreePath).Returns(worktreeGitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        stackActions.PullChanges(stack);

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
        var gitHubClient = Substitute.For<IGitHubClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

        var statuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 5, new Commit(Some.Sha(), Some.Name())) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(statuses);

        var stack = new TestStackBuilder().WithSourceBranch(sourceBranch).Build();

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        stackActions.PullChanges(stack);

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
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

        gitClient.GetCurrentBranch().Returns(sourceBranch);
        var statuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 1, new Commit(Some.Sha(), Some.Name())) },
            { otherBranch, new GitBranchStatus(otherBranch, $"origin/{otherBranch}", true, false, 0, 2, new Commit(Some.Sha(), Some.Name())) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(statuses);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(otherBranch))
            .Build();

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        stackActions.PullChanges(stack);

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
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

        gitClient.GetCurrentBranch().Returns(sourceBranch);
        var statuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { otherBranch, new GitBranchStatus(otherBranch, $"origin/{otherBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(statuses);
        var stack = new TestStackBuilder().WithSourceBranch(sourceBranch).WithBranch(b => b.WithName(otherBranch)).Build();

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        stackActions.PullChanges(stack);

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

        var defaultGitClient = Substitute.For<IGitClient>();
        var worktreeGitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

        defaultGitClient.GetCurrentBranch().Returns(sourceBranch);
        var statuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 0, new Commit(Some.Sha(), Some.Name()), null) },
            { branchInOtherWorktree, new GitBranchStatus(branchInOtherWorktree, $"origin/{branchInOtherWorktree}", true, false, 0, 4, new Commit(Some.Sha(), Some.Name()), worktreePath) }
        };
        defaultGitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(statuses);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchInOtherWorktree))
            .Build();

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(executionContext.WorkingDirectory).Returns(defaultGitClient);
        factory.Create(worktreePath).Returns(worktreeGitClient);

        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        stackActions.PullChanges(stack);

        // Assert
        defaultGitClient.DidNotReceive().FetchBranchRefSpecs(Arg.Is<string[]>(arr => arr.Contains(branchInOtherWorktree)));
        worktreeGitClient.Received(1).PullBranch(branchInOtherWorktree);
    }

    [Fact]
    public void PushChanges_WhenSomeLocalBranchesAreAhead_OnlyPushesChangesForBranchesThatAreAhead()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchAheadOfRemote = Some.BranchName();
        var branchNotAheadOfRemote = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

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

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        stackActions.PushChanges(stack, 5, false);

        // Assert
        branchesPushedToRemote.ToArray().Should().BeEquivalentTo([branchAheadOfRemote]);
    }

    [Fact]
    public void PushChanges_WhenSomeBranchesDoNotExistInRemote_OnlyPushesBranchesThatExistInRemote()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchThatExistsInRemoteAndIsAhead = Some.BranchName();
        var branchThatDoesNotExistInRemoteButIsAhead = Some.BranchName();

        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

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

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

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
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

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

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

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
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

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

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

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
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

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

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

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
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

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

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var factory = Substitute.For<IGitClientFactory>();
        factory.Create(Arg.Any<string>()).Returns(gitClient);
        var stackActions = new StackActions(factory, executionContext, gitHubClient, logger, console);

        // Act
        stackActions.PushChanges(stack, maxBatchSize: 5, forceWithLease: false);

        // Assert
        gitClient.DidNotReceive().PushBranches(Arg.Any<string[]>(), Arg.Any<bool>());
        gitClient.DidNotReceive().PushNewBranch(Arg.Any<string>());
    }

    [Fact]
    public async Task UpdateStack_UsingMerge_WhenBranchIsInWorktree_UsesWorktreeGitClient()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchInWorktree = Some.BranchName();
        var worktreePath = $"C:/temp/{Some.Name()}";

        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var worktreeGitClient = Substitute.For<IGitClient>();

        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClientFactory.Create(worktreePath).Returns(worktreeGitClient);

        var branchStatuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 0, new Commit(Some.Sha(), Some.Name()), null) },
            { branchInWorktree, new GitBranchStatus(branchInWorktree, $"origin/{branchInWorktree}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name()), worktreePath) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatuses);

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            new List<Config.Branch> { new Config.Branch(branchInWorktree, new List<Config.Branch>()) }
        );

        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var stackActions = new StackActions(gitClientFactory, executionContext, gitHubClient, logger, console);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Merge, CancellationToken.None);

        // Assert
        gitClientFactory.Received(1).Create(worktreePath);
        worktreeGitClient.Received(1).MergeFromLocalSourceBranch(sourceBranch);
        gitClient.DidNotReceive().ChangeBranch(branchInWorktree); // Should not change branch since it's in a worktree
    }

    [Fact]
    public async Task UpdateStack_UsingRebase_WhenBranchIsInWorktree_UsesWorktreeGitClient()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchInWorktree = Some.BranchName();
        var worktreePath = $"C:/temp/{Some.Name()}";

        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var worktreeGitClient = Substitute.For<IGitClient>();

        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClientFactory.Create(worktreePath).Returns(worktreeGitClient);

        var branchStatuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 0, new Commit(Some.Sha(), Some.Name()), null) },
            { branchInWorktree, new GitBranchStatus(branchInWorktree, $"origin/{branchInWorktree}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name()), worktreePath) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatuses);

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            new List<Config.Branch> { new Config.Branch(branchInWorktree, new List<Config.Branch>()) }
        );

        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var stackActions = new StackActions(gitClientFactory, executionContext, gitHubClient, logger, console);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert
        gitClientFactory.Received(1).Create(worktreePath);
        worktreeGitClient.Received(1).RebaseFromLocalSourceBranch(sourceBranch);
        gitClient.DidNotReceive().ChangeBranch(branchInWorktree); // Should not change branch since it's in a worktree
    }
}