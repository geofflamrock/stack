using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Meziantou.Extensions.Logging.Xunit;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Infrastructure;
using Xunit.Abstractions;

namespace Stack.Tests.Helpers;

public class StackActionsTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task UpdateStack_UsingMerge_WhenThereAreConflictsMergingBranches_AndUpdateIsContinued_TheUpdateCompletesSuccessfully()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
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
                Arg.Any<CancellationToken>(),
                Arg.Any<Func<MergeConflictAction, string>>())
            .Returns(MergeConflictAction.Continue);

        gitClient
            .When(g => g.MergeFromLocalSourceBranch(sourceBranch))
            .Throws(new ConflictException());

        var stackActions = new StackActions(
            gitClient,
            gitHubClient,
            inputProvider,
            logger,
            console
        );

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Merge, CancellationToken.None);

        // Assert
        gitClient.Received().ChangeBranch(branch2);
        gitClient.Received().MergeFromLocalSourceBranch(branch1);
    }

    [Fact]
    public async Task UpdateStack_UsingMerge_WhenThereAreConflictsMergingBranches_AndUpdateIsAborted_AnExceptionIsThrown()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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
                Arg.Any<CancellationToken>(),
                Arg.Any<Func<MergeConflictAction, string>>())
            .Returns(MergeConflictAction.Abort);

        var stackActions = new StackActions(
            gitClient,
            gitHubClient,
            inputProvider,
            logger,
            console
        );

        // Act
        var updateAction = async () => await stackActions.UpdateStack(stack, UpdateStrategy.Merge, CancellationToken.None);

        // Assert
        await updateAction.Should().ThrowAsync<Exception>().WithMessage("Merge aborted due to conflicts.");
        gitClient.Received().AbortMerge();
        gitClient.DidNotReceive().ChangeBranch(branch2);
        gitClient.DidNotReceive().MergeFromLocalSourceBranch(branch1);
    }

    [Fact]
    public async Task UpdateStack_UsingRebase_WhenThereAreConflictsMergingBranches_AndUpdateIsContinued_TheUpdateCompletesSuccessfully()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
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
                Arg.Any<CancellationToken>(),
                Arg.Any<Func<MergeConflictAction, string>>())
            .Returns(MergeConflictAction.Continue);

        gitClient
            .When(g => g.RebaseFromLocalSourceBranch(sourceBranch))
            .Throws(new ConflictException());

        var stackActions = new StackActions(
            gitClient,
            gitHubClient,
            inputProvider,
            logger,
            console
        );

        // Act
        await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert
        gitClient.Received().ChangeBranch(branch2);
        gitClient.Received().RebaseFromLocalSourceBranch(sourceBranch);
    }

    [Fact]
    public async Task UpdateStack_UsingRebase_WhenThereAreConflictsMergingBranches_AndUpdateIsAborted_AnExceptionIsThrown()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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
                Arg.Any<CancellationToken>(),
                Arg.Any<Func<MergeConflictAction, string>>())
            .Returns(MergeConflictAction.Abort);

        var stackActions = new StackActions(
            gitClient,
            gitHubClient,
            inputProvider,
            logger,
            console
        );

        // Act
        var updateAction = async () => await stackActions.UpdateStack(stack, UpdateStrategy.Rebase, CancellationToken.None);

        // Assert
        await updateAction.Should().ThrowAsync<Exception>().WithMessage("Rebase aborted due to conflicts.");
        gitClient.Received().AbortRebase();
    }

    [Fact]
    public async Task UpdateStack_UsingRebase_WhenARemoteBranchIsDeleted_RebasesOntoTheParentBranchToAvoidConflicts()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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
            inputProvider,
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

        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
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

        var console = new TestAnsiConsoleWriter(testOutputHelper);
        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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

        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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

        var inputProvider = Substitute.For<IInputProvider>();
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

        var statuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 5, new Commit(Some.Sha(), Some.Name())) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(statuses);

        var stack = new TestStackBuilder().WithSourceBranch(sourceBranch).Build();

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

        gitClient.GetCurrentBranch().Returns(sourceBranch);
        var statuses = new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, true, 0, 0, new Commit(Some.Sha(), Some.Name())) },
            { otherBranch, new GitBranchStatus(otherBranch, $"origin/{otherBranch}", true, false, 0, 0, new Commit(Some.Sha(), Some.Name())) }
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(statuses);
        var stack = new TestStackBuilder().WithSourceBranch(sourceBranch).WithBranch(b => b.WithName(otherBranch)).Build();

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

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
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var console = new TestAnsiConsoleWriter(testOutputHelper);

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

        var stackActions = new StackActions(gitClient, gitHubClient, inputProvider, logger, console);

        // Act
        stackActions.PushChanges(stack, maxBatchSize: 5, forceWithLease: false);

        // Assert
        gitClient.DidNotReceive().PushBranches(Arg.Any<string[]>(), Arg.Any<bool>());
        gitClient.DidNotReceive().PushNewBranch(Arg.Any<string>());
    }
}