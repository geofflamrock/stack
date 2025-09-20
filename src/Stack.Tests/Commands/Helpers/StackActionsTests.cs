using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Meziantou.Extensions.Logging.Xunit;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;
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
        var actions = new StackActions(gitClient, gitHubClient, logger, console);

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
        var actions = new StackActions(gitClient, gitHubClient, logger, console);

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
        var actions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var actions = new StackActions(gitClient, gitHubClient, logger, console);

        // Act
        await actions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert
        gitClient.Received().ChangeBranch(feature);
    }

    [Fact]
    public async Task UpdateStack_UsingRebase_WhenARemoteBranchIsDeleted_RebasesOntoTheParentBranchToAvoidConflicts()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

        // Setup branch statuses to simulate the scenario
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", false, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }, // remote branch deleted
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        });
        gitClient.IsAncestor(branch2, branch1).Returns(true);

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()) }) }
        );

        var stackActions = new StackActions(
            gitClient,
            gitHubClient,
            logger,
            console
        );

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert
        gitClient.Received().ChangeBranch(branch2);
        gitClient.Received().RebaseOntoNewParent(sourceBranch, branch1);
    }

    [Fact]
    public async Task UpdateStack_UsingRebase_WhenARemoteBranchIsDeleted_ButTheTargetBranchHasAlreadyHadAdditionalCommitsMergedInto_DoesNotRebaseOntoTheParentBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var changedFilePath = Some.Name();

        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

        // Setup branch statuses to simulate the scenario
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", false, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }, // remote branch deleted
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        });
        gitClient.IsAncestor(branch2, branch1).Returns(false);

        var stack = new Config.Stack(
            "Stack1",
            Some.HttpsUri().ToString(),
            sourceBranch,
            new List<Config.Branch> { new Config.Branch(branch1, new List<Config.Branch> { new Config.Branch(branch2, new List<Config.Branch>()) }) }
        );

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert
        gitClient.Received().ChangeBranch(branch2);
        gitClient.Received().RebaseFromLocalSourceBranch(sourceBranch);
    }

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
        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

        // Act
        stackActions.PullChanges(stack);

        // Assert
        gitClient.Received(1).PullBranchForWorktree(branchInOtherWorktree, worktreePath);
        gitClient.DidNotReceive().FetchBranchRefSpecs(Arg.Any<string[]>());
        gitClient.DidNotReceive().PullBranch(branchInOtherWorktree);
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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

        // Act
        stackActions.PushChanges(stack, maxBatchSize: 5, forceWithLease: false);

        // Assert
        gitClient.DidNotReceive().PushBranches(Arg.Any<string[]>(), Arg.Any<bool>());
        gitClient.DidNotReceive().PushNewBranch(Arg.Any<string>());
    }

    public class IntegrationTests(ITestOutputHelper testOutputHelper)
    {
        [Fact]
        public void PullChanges_WithRealGitRepository_PullsChangesForSourceBranch()
        {
            // Arrange
            var sourceBranch = Some.BranchName();
            var otherBranch = Some.BranchName();

            using var repo = new TestGitRepositoryBuilder()
                .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .WithBranch(builder => builder.WithName(otherBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .Build();

            var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            // Create commits on remote tracking branches to simulate changes to pull
            repo.CreateCommitOnRemoteTrackingBranch(sourceBranch, "Remote change on source");
            repo.CreateCommitOnRemoteTrackingBranch(otherBranch, "Remote change on other");

            // Make source branch current
            gitClient.ChangeBranch(sourceBranch);

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(otherBranch))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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
        public void PullChanges_WithRealGitRepository_PullsChangesForCurrentBranch()
        {
            // Arrange
            var sourceBranch = Some.BranchName();
            var featureBranch = Some.BranchName();

            using var repo = new TestGitRepositoryBuilder()
                .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .WithBranch(builder => builder.WithName(featureBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .Build();

            var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            // Create commits on remote tracking branch to simulate changes to pull
            repo.CreateCommitOnRemoteTrackingBranch(featureBranch, "Remote change on feature");

            // Make feature branch current
            gitClient.ChangeBranch(featureBranch);

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(featureBranch))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

            // Act
            stackActions.PullChanges(stack);
            
            // Assert - verify that local branch points to correct SHA from remote branch
            var featureLocalTip = repo.GetTipOfBranch(featureBranch);
            var featureRemoteTip = repo.GetTipOfRemoteBranch(featureBranch);
            featureLocalTip.Sha.Should().Be(featureRemoteTip.Sha, "current branch should be pulled to match remote");
        }

        [Fact]
        public void PullChanges_WithRealGitRepository_FetchesChangesForNonCurrentBranch()
        {
            // Arrange
            var sourceBranch = Some.BranchName();
            var featureBranch = Some.BranchName();

            using var repo = new TestGitRepositoryBuilder()
                .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .WithBranch(builder => builder.WithName(featureBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .Build();

            var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            // Create commits on remote tracking branch to simulate changes to pull
            repo.CreateCommitOnRemoteTrackingBranch(featureBranch, "Remote change on feature");

            // Make source branch current (so feature is non-current)
            gitClient.ChangeBranch(sourceBranch);

            // Get the branch status before pull to verify there are changes behind
            var statusBefore = gitClient.GetBranchStatuses([featureBranch]);
            statusBefore[featureBranch].Behind.Should().BeGreaterThan(0, "branch should be behind remote before pull");

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(featureBranch))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

            // Act
            stackActions.PullChanges(stack);

            // Assert - verify that fetch operation updated the remote tracking references
            // For non-current branches, the fetch updates the remote tracking branch reference
            var featureLocalTip = repo.GetTipOfBranch(featureBranch);
            var featureRemoteTip = repo.GetTipOfRemoteBranch(featureBranch);
            // The local branch should have been updated via fetch to match the remote
            featureLocalTip.Sha.Should().Be(featureRemoteTip.Sha, "non-current branch should be fetched to match remote");
        }

        [Fact]
        public void PullChanges_WithRealGitRepository_PullsBranchCheckedOutInWorktree()
        {
            // Arrange
            var sourceBranch = Some.BranchName();
            var worktreeBranch = Some.BranchName();

            using var repo = new TestGitRepositoryBuilder()
                .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .WithBranch(builder => builder.WithName(worktreeBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .Build();

            var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            // Create commits on remote tracking branch to simulate changes to pull  
            repo.CreateCommitOnRemoteTrackingBranch(worktreeBranch, "Remote change on worktree branch");

            // Switch to source branch first, then create worktree (can't create worktree for current branch)
            gitClient.ChangeBranch(sourceBranch);
            
            // Create a worktree for the branch
            var worktreePath = repo.CreateWorktree(worktreeBranch);

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(worktreeBranch))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

            // Act
            stackActions.PullChanges(stack);

            // Assert - verify that branch in worktree was pulled correctly
            var worktreeLocalTip = repo.GetTipOfBranch(worktreeBranch);
            var worktreeRemoteTip = repo.GetTipOfRemoteBranch(worktreeBranch);
            worktreeLocalTip.Sha.Should().Be(worktreeRemoteTip.Sha, "worktree branch should be pulled to match remote");
        }

        [Fact]
        public void PullChanges_WithRealGitRepository_SkipsBranchWithNoRemoteTrackingBranch()
        {
            // Arrange
            var sourceBranch = Some.BranchName();
            var localOnlyBranch = Some.BranchName();

            using var repo = new TestGitRepositoryBuilder()
                .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .WithBranch(builder => builder.WithName(localOnlyBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(1)) // No PushToRemote
                .Build();

            var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            gitClient.ChangeBranch(sourceBranch);
            var initialLocalTip = repo.GetTipOfBranch(localOnlyBranch);

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(localOnlyBranch))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

            // Act
            stackActions.PullChanges(stack);

            // Assert - local-only branch should not have been affected
            var finalLocalTip = repo.GetTipOfBranch(localOnlyBranch);
            finalLocalTip.Sha.Should().Be(initialLocalTip.Sha, "local-only branch should remain unchanged");
        }

        [Fact]
        public void PullChanges_WithRealGitRepository_SkipsBranchWithDeletedRemoteTrackingBranch()
        {
            // Arrange
            var sourceBranch = Some.BranchName();
            var deletedRemoteBranch = Some.BranchName();

            using var repo = new TestGitRepositoryBuilder()
                .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .WithBranch(builder => builder.WithName(deletedRemoteBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .Build();

            var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            // Delete the remote tracking branch to simulate deleted remote
            repo.DeleteRemoteTrackingBranch(deletedRemoteBranch);

            gitClient.ChangeBranch(sourceBranch);
            var initialLocalTip = repo.GetTipOfBranch(deletedRemoteBranch);

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(deletedRemoteBranch))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

            // Act
            stackActions.PullChanges(stack);

            // Assert - branch with deleted remote should not be modified
            var finalLocalTip = repo.GetTipOfBranch(deletedRemoteBranch);
            finalLocalTip.Sha.Should().Be(initialLocalTip.Sha, "branch with deleted remote should remain unchanged");
        }

        [Fact]
        public async Task UpdateStack_WithRealGitRepository_UpdatesUsingMergeWithMultipleBranchLines()
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
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            // Add changes to source branch (simulating changes to merge)
            gitClient.ChangeBranch(sourceBranch);
            var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
            File.WriteAllText(filePath, "source change");
            repo.Stage(Path.GetFileName(filePath));
            var sourceCommit = repo.Commit("Source branch change");

            // Add changes to line1Branch1 (simulating changes at multiple levels)
            gitClient.ChangeBranch(line1Branch1);
            var filePath2 = Path.Join(repo.LocalDirectoryPath, Some.Name());
            File.WriteAllText(filePath2, "line1 change");
            repo.Stage(Path.GetFileName(filePath2));
            var line1Commit = repo.Commit("Line1 branch change");

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(line1Branch1).WithChildBranch(c => c.WithName(line1Branch2)))
                .WithBranch(b => b.WithName(line2Branch1))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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
        public async Task UpdateStack_WithRealGitRepository_UpdatesUsingRebaseWithMultipleBranchLines()
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
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            // Add changes to source branch (simulating changes to rebase onto)
            gitClient.ChangeBranch(sourceBranch);
            var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
            File.WriteAllText(filePath, "source change");
            repo.Stage(Path.GetFileName(filePath));
            var sourceCommit = repo.Commit("Source branch change");

            // Add changes to line1Branch1 (simulating changes at multiple levels)
            gitClient.ChangeBranch(line1Branch1);
            var filePath2 = Path.Join(repo.LocalDirectoryPath, Some.Name());
            File.WriteAllText(filePath2, "line1 change");
            repo.Stage(Path.GetFileName(filePath2));
            var line1Commit = repo.Commit("Line1 branch change");

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(line1Branch1).WithChildBranch(c => c.WithName(line1Branch2)))
                .WithBranch(b => b.WithName(line2Branch1))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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
        public void PushChanges_WithRealGitRepository_PushesCurrentBranch()
        {
            // Arrange
            var sourceBranch = Some.BranchName();
            var currentBranch = Some.BranchName();

            using var repo = new TestGitRepositoryBuilder()
                .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .WithBranch(builder => builder.WithName(currentBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .Build();

            var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            // Make changes to the current branch
            gitClient.ChangeBranch(currentBranch);
            var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
            File.WriteAllText(filePath, "local change");
            repo.Stage(Path.GetFileName(filePath));
            var localCommit = repo.Commit("Local change on current branch");

            var initialRemoteCommitCount = repo.GetCommitsReachableFromRemoteBranch(currentBranch).Count;

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(currentBranch))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

            // Act
            stackActions.PushChanges(stack, 5, false);

            // Assert - remote branch should be at same SHA as local branch
            var localTip = repo.GetTipOfBranch(currentBranch);
            var remoteTip = repo.GetTipOfRemoteBranch(currentBranch);
            remoteTip.Sha.Should().Be(localTip.Sha, "remote branch should be at same SHA as local branch after push");
        }

        [Fact]
        public void PushChanges_WithRealGitRepository_PushesNonCurrentBranch()
        {
            // Arrange
            var sourceBranch = Some.BranchName();
            var nonCurrentBranch = Some.BranchName();

            using var repo = new TestGitRepositoryBuilder()
                .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .WithBranch(builder => builder.WithName(nonCurrentBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .Build();

            var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            // Make changes to the non-current branch
            gitClient.ChangeBranch(nonCurrentBranch);
            var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
            File.WriteAllText(filePath, "local change");
            repo.Stage(Path.GetFileName(filePath));
            var localCommit = repo.Commit("Local change on non-current branch");

            // Switch to source branch so nonCurrentBranch is non-current
            gitClient.ChangeBranch(sourceBranch);
            var initialRemoteCommitCount = repo.GetCommitsReachableFromRemoteBranch(nonCurrentBranch).Count;

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(nonCurrentBranch))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

            // Act
            stackActions.PushChanges(stack, 5, false);

            // Assert - remote branch should be at same SHA as local branch after push
            var localTip = repo.GetTipOfBranch(nonCurrentBranch);
            var remoteTip = repo.GetTipOfRemoteBranch(nonCurrentBranch);
            remoteTip.Sha.Should().Be(localTip.Sha, "remote branch should match local branch SHA after push");
        }

        [Fact]
        public void PushChanges_WithRealGitRepository_PushesBranchCheckedOutInWorktree()
        {
            // Arrange
            var sourceBranch = Some.BranchName();
            var worktreeBranch = Some.BranchName();

            using var repo = new TestGitRepositoryBuilder()
                .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .WithBranch(builder => builder.WithName(worktreeBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .Build();

            var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            // Make changes to the worktree branch, then switch away to create worktree
            gitClient.ChangeBranch(worktreeBranch);
            var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
            File.WriteAllText(filePath, "worktree branch change");
            repo.Stage(Path.GetFileName(filePath));
            var branchCommit = repo.Commit("Change in worktree branch");

            // Switch to source branch first, then create worktree (can't create worktree for current branch)
            gitClient.ChangeBranch(sourceBranch);
            
            // Create a worktree for the branch
            var worktreePath = repo.CreateWorktree(worktreeBranch);

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(worktreeBranch))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

            // Act
            stackActions.PushChanges(stack, 5, false);

            // Assert - verify that branch in worktree was pushed correctly
            var worktreeLocalTip = repo.GetTipOfBranch(worktreeBranch);
            var worktreeRemoteTip = repo.GetTipOfRemoteBranch(worktreeBranch);
            worktreeRemoteTip.Sha.Should().Be(worktreeLocalTip.Sha, "worktree branch should be pushed to match local branch");
        }

        [Fact]
        public void PushChanges_WithRealGitRepository_CreatesRemoteTrackingBranchForLocalOnlyBranch()
        {
            // Arrange
            var sourceBranch = Some.BranchName();
            var localOnlyBranch = Some.BranchName();

            using var repo = new TestGitRepositoryBuilder()
                .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .WithBranch(builder => builder.WithName(localOnlyBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(1)) // No PushToRemote
                .Build();

            var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            // Make changes to the local-only branch
            gitClient.ChangeBranch(localOnlyBranch);
            var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
            File.WriteAllText(filePath, "local change");
            repo.Stage(Path.GetFileName(filePath));
            var localCommit = repo.Commit("Local change on local-only branch");

            gitClient.ChangeBranch(sourceBranch);

            // Verify initially no remote tracking branch
            var initialHasRemoteTracking = repo.DoesRemoteBranchExist(localOnlyBranch);
            initialHasRemoteTracking.Should().BeFalse("local-only branch should initially not have remote tracking branch");

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(localOnlyBranch))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

            // Act - should complete without errors and should actually create the remote tracking branch
            stackActions.PushChanges(stack, 5, false);

            // Assert - verify branch exists locally and now HAS a remote tracking branch (because PushChanges creates it)
            var branchExists = gitClient.DoesLocalBranchExist(localOnlyBranch);
            var finalHasRemoteTracking = repo.DoesRemoteBranchExist(localOnlyBranch);
            
            branchExists.Should().BeTrue("local branch should still exist");
            finalHasRemoteTracking.Should().BeTrue("PushChanges should create remote tracking branch for branches without one");
            
            // Also assert that the remote branch has the correct SHA from the local branch
            var localTip = repo.GetTipOfBranch(localOnlyBranch);
            var remoteTip = repo.GetTipOfRemoteBranch(localOnlyBranch);
            remoteTip.Sha.Should().Be(localTip.Sha, "remote branch should have same SHA as local branch");
        }

        [Fact]
        public void PushChanges_WithRealGitRepository_SkipsBranchWithDeletedRemoteTrackingBranch()
        {
            // Arrange
            var sourceBranch = Some.BranchName();
            var deletedRemoteBranch = Some.BranchName();

            using var repo = new TestGitRepositoryBuilder()
                .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .WithBranch(builder => builder.WithName(deletedRemoteBranch).FromSourceBranch(sourceBranch).PushToRemote().WithNumberOfEmptyCommits(1))
                .Build();

            var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
            var gitClientLogger = XUnitLogger.CreateLogger<GitClient>(testOutputHelper);
            var console = new TestDisplayProvider(testOutputHelper);
            var gitClient = new GitClient(gitClientLogger, repo.ExecutionContext);
            var gitHubClient = Substitute.For<IGitHubClient>();

            // Make changes to the branch
            gitClient.ChangeBranch(deletedRemoteBranch);
            var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
            File.WriteAllText(filePath, "local change");
            repo.Stage(Path.GetFileName(filePath));
            var localCommit = repo.Commit("Local change on branch");

            // Delete the remote tracking branch to simulate deleted remote
            repo.DeleteRemoteTrackingBranch(deletedRemoteBranch);

            gitClient.ChangeBranch(sourceBranch);
            var initialLocalCommitCount = repo.GetCommitsReachableFromBranch(deletedRemoteBranch).Count;

            var stack = new TestStackBuilder()
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(deletedRemoteBranch))
                .Build();

            var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

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
}