using System.IO;
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

namespace Stack.Tests.Commands.Helpers;

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

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(sourceBranch).PushToRemote())
            .WithBranch(b => b.WithName(branchWithRemoteChanges).PushToRemote())
            .WithBranch(b => b.WithName(branchWithoutRemoteChanges).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var gitClient = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.ExecutionContext);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        
        // Set up scenario: only one branch has remote changes
        gitClient.ChangeBranch(sourceBranch);
        repo.CreateCommitOnRemoteTrackingBranch(branchWithRemoteChanges, "Remote commit on branch with changes");
        // branchWithoutRemoteChanges remains up to date

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchWithRemoteChanges))
            .WithBranch(b => b.WithName(branchWithoutRemoteChanges))
            .Build();

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

        // Verify initial state
        var initialStatuses = gitClient.GetBranchStatuses([sourceBranch, branchWithRemoteChanges, branchWithoutRemoteChanges]);
        initialStatuses[sourceBranch].Behind.Should().Be(0, "source branch should be up to date");
        initialStatuses[branchWithRemoteChanges].Behind.Should().BeGreaterThan(0, "branch should be behind remote");
        initialStatuses[branchWithoutRemoteChanges].Behind.Should().Be(0, "branch should be up to date");

        // Act
        stackActions.PullChanges(stack);

        // Assert - Only the branch with remote changes should be fetched
        var finalStatuses = gitClient.GetBranchStatuses([sourceBranch, branchWithRemoteChanges, branchWithoutRemoteChanges]);
        finalStatuses[branchWithRemoteChanges].Behind.Should().Be(0, "branch with remote changes should be fetched");
        finalStatuses[branchWithoutRemoteChanges].Behind.Should().Be(0, "branch without changes should remain up to date");
        finalStatuses[sourceBranch].Behind.Should().Be(0, "source branch should remain up to date");
    }

    [Fact]
    public void PullChanges_WhenSomeBranchesDoNotExistInRemote_OnlyPullsBranchesThatExistInRemote()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchThatExistsInRemote = Some.BranchName();
        var branchThatDoesNotExistInRemote = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(sourceBranch).PushToRemote())
            .WithBranch(b => b.WithName(branchThatExistsInRemote).PushToRemote())
            .WithBranch(b => b.WithName(branchThatDoesNotExistInRemote)) // Not pushed to remote
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var gitClient = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.ExecutionContext);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        
        // Set up scenario: make the branch that exists in remote behind
        gitClient.ChangeBranch(sourceBranch);
        repo.CreateCommitOnRemoteTrackingBranch(branchThatExistsInRemote, "Remote commit on branch that exists");

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchThatExistsInRemote))
            .WithBranch(b => b.WithName(branchThatDoesNotExistInRemote))
            .Build();

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

        // Verify initial state
        var initialStatuses = gitClient.GetBranchStatuses([sourceBranch, branchThatExistsInRemote, branchThatDoesNotExistInRemote]);
        initialStatuses[branchThatExistsInRemote].RemoteBranchExists.Should().BeTrue("branch should exist in remote");
        initialStatuses[branchThatExistsInRemote].Behind.Should().BeGreaterThan(0, "branch should be behind remote");
        initialStatuses[branchThatDoesNotExistInRemote].RemoteBranchExists.Should().BeFalse("branch should not exist in remote");

        // Act
        stackActions.PullChanges(stack);

        // Assert - Only the branch that exists in remote should be fetched
        var finalStatuses = gitClient.GetBranchStatuses([sourceBranch, branchThatExistsInRemote, branchThatDoesNotExistInRemote]);
        finalStatuses[branchThatExistsInRemote].Behind.Should().Be(0, "branch that exists in remote should be fetched");
        finalStatuses[branchThatDoesNotExistInRemote].Behind.Should().Be(0, "branch with no remote should remain unchanged");
    }

    [Fact]
    public void PullChanges_WhenOnlyNonCurrentBranchesBehind_FetchesThem()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(sourceBranch).PushToRemote())
            .WithBranch(b => b.WithName(branch1).PushToRemote())
            .WithBranch(b => b.WithName(branch2).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var gitClient = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.ExecutionContext);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        
        // Set up scenario: source branch is current and up to date, other branches are behind
        gitClient.ChangeBranch(sourceBranch);
        
        // Make non-current branches behind by creating commits on their remote tracking branches
        repo.CreateCommitOnRemoteTrackingBranch(branch1, "Remote commit on branch1");
        repo.CreateCommitOnRemoteTrackingBranch(branch2, "Remote commit on branch2");

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branch1))
            .WithBranch(b => b.WithName(branch2))
            .Build();

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

        // Verify initial state
        var initialStatuses = gitClient.GetBranchStatuses([sourceBranch, branch1, branch2]);
        initialStatuses[sourceBranch].Behind.Should().Be(0, "source branch should be up to date");
        initialStatuses[sourceBranch].IsCurrentBranch.Should().BeTrue();
        initialStatuses[branch1].Behind.Should().BeGreaterThan(0, "branch1 should be behind");
        initialStatuses[branch1].IsCurrentBranch.Should().BeFalse();
        initialStatuses[branch2].Behind.Should().BeGreaterThan(0, "branch2 should be behind");
        initialStatuses[branch2].IsCurrentBranch.Should().BeFalse();

        // Act
        stackActions.PullChanges(stack);

        // Assert - Non-current branches should be fetched and up to date
        var finalStatuses = gitClient.GetBranchStatuses([sourceBranch, branch1, branch2]);
        finalStatuses[sourceBranch].Behind.Should().Be(0, "source branch should remain up to date");
        finalStatuses[branch1].Behind.Should().Be(0, "branch1 should be fetched up to date");
        finalStatuses[branch2].Behind.Should().Be(0, "branch2 should be fetched up to date");
        
        // Source branch should still be current
        gitClient.GetCurrentBranch().Should().Be(sourceBranch);
    }

    [Fact]
    public void PullChanges_WhenOnlyCurrentBranchBehind_PullsIt()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(sourceBranch).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var gitClient = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.ExecutionContext);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        
        // Set up the scenario: local branch is behind remote
        gitClient.ChangeBranch(sourceBranch);
        
        // Create a commit on the remote tracking branch to make local behind
        repo.CreateCommitOnRemoteTrackingBranch(sourceBranch, "Remote commit that makes local behind");
        
        var stack = new TestStackBuilder().WithSourceBranch(sourceBranch).Build();
        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

        // Verify initial state - local should be behind remote
        var branchStatuses = gitClient.GetBranchStatuses([sourceBranch]);
        branchStatuses[sourceBranch].Behind.Should().BeGreaterThan(0);
        branchStatuses[sourceBranch].IsCurrentBranch.Should().BeTrue();

        // Act
        stackActions.PullChanges(stack);

        // Assert - After pulling, the branch should be up to date
        var updatedStatuses = gitClient.GetBranchStatuses([sourceBranch]);
        updatedStatuses[sourceBranch].Behind.Should().Be(0);
    }

    [Fact]
    public void PullChanges_WhenCurrentAndOtherBranchesBehind_PullsCurrentAndFetchesOthers()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var otherBranch = Some.BranchName();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(sourceBranch).PushToRemote())
            .WithBranch(b => b.WithName(otherBranch).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var gitClient = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.ExecutionContext);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        
        // Set up scenario: both branches are behind their remotes
        gitClient.ChangeBranch(sourceBranch);
        
        // Make both branches behind their remotes by creating commits on remote tracking branches
        repo.CreateCommitOnRemoteTrackingBranch(sourceBranch, "Remote commit on source branch");
        repo.CreateCommitOnRemoteTrackingBranch(otherBranch, "Remote commit on other branch");
        
        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(otherBranch))
            .Build();

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

        // Verify initial state - both branches should be behind
        var initialStatuses = gitClient.GetBranchStatuses([sourceBranch, otherBranch]);
        initialStatuses[sourceBranch].Behind.Should().BeGreaterThan(0);
        initialStatuses[sourceBranch].IsCurrentBranch.Should().BeTrue();
        initialStatuses[otherBranch].Behind.Should().BeGreaterThan(0);
        initialStatuses[otherBranch].IsCurrentBranch.Should().BeFalse();

        // Act
        stackActions.PullChanges(stack);

        // Assert - Current branch should be up to date, other branch fetched but not pulled
        var finalStatuses = gitClient.GetBranchStatuses([sourceBranch, otherBranch]);
        finalStatuses[sourceBranch].Behind.Should().Be(0, "current branch should be pulled up to date");
        finalStatuses[otherBranch].Behind.Should().Be(0, "other branch should be fetched up to date");
    }

    [Fact]
    public void PullChanges_WhenNoBranchesBehind_DoesNothing()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var otherBranch = Some.BranchName();
        
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(sourceBranch).PushToRemote())
            .WithBranch(b => b.WithName(otherBranch).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var gitClient = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.ExecutionContext);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        
        // Ensure we're on the source branch
        gitClient.ChangeBranch(sourceBranch);
        
        var stack = new TestStackBuilder().WithSourceBranch(sourceBranch).WithBranch(b => b.WithName(otherBranch)).Build();
        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

        // Act
        stackActions.PullChanges(stack);

        // Assert - Since both branches are up to date (ahead=0, behind=0), no pull operations should occur
        // We can't easily assert that no git operations happened, but we can verify the state remains unchanged
        var currentBranch = gitClient.GetCurrentBranch();
        currentBranch.Should().Be(sourceBranch);
    }

    [Fact]
    public void PullChanges_WhenBranchIsBehind_AndCheckedOutInAnotherWorktree_PullsItDirectly()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchInOtherWorktree = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(sourceBranch).PushToRemote())
            .WithBranch(b => b.WithName(branchInOtherWorktree).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var gitClient = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.ExecutionContext);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        
        // Set up scenario: source branch is current, other branch is in a worktree and behind
        gitClient.ChangeBranch(sourceBranch);
        
        // Create a worktree for the branch
        var worktreePath = repo.CreateWorktree(branchInOtherWorktree);
        
        // Make the branch in worktree behind by creating a commit on remote
        repo.CreateCommitOnRemoteTrackingBranch(branchInOtherWorktree, "Remote commit on worktree branch");

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchInOtherWorktree))
            .Build();

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

        // Verify initial state
        var initialStatuses = gitClient.GetBranchStatuses([sourceBranch, branchInOtherWorktree]);
        initialStatuses[branchInOtherWorktree].Behind.Should().BeGreaterThan(0, "worktree branch should be behind");
        initialStatuses[branchInOtherWorktree].WorktreePath.Should().NotBeNullOrEmpty("branch should be in worktree");

        // Act
        stackActions.PullChanges(stack);

        // Assert - The worktree branch should be pulled directly in its worktree
        var finalStatuses = gitClient.GetBranchStatuses([sourceBranch, branchInOtherWorktree]);
        finalStatuses[branchInOtherWorktree].Behind.Should().Be(0, "worktree branch should be pulled up to date");
    }

    [Fact]
    public void PushChanges_WhenSomeLocalBranchesAreAhead_OnlyPushesChangesForBranchesThatAreAhead()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchAheadOfRemote = Some.BranchName();
        var branchNotAheadOfRemote = Some.BranchName();

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(sourceBranch).PushToRemote())
            .WithBranch(b => b.WithName(branchAheadOfRemote).PushToRemote())
            .WithBranch(b => b.WithName(branchNotAheadOfRemote).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var gitClient = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.ExecutionContext);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        
        // Set up scenario: one branch is ahead of remote, others are up to date
        gitClient.ChangeBranch(branchAheadOfRemote);
        
        // Create local commits to make this branch ahead
        var filePath = Path.Join(repo.LocalDirectoryPath, Some.Name());
        File.WriteAllText(filePath, Some.Name());
        repo.Stage(Path.GetFileName(filePath));
        repo.Commit("Local commit making branch ahead");
        
        // Switch back to source branch
        gitClient.ChangeBranch(sourceBranch);

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchAheadOfRemote))
            .WithBranch(b => b.WithName(branchNotAheadOfRemote))
            .Build();

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

        // Verify initial state
        var initialStatuses = gitClient.GetBranchStatuses([sourceBranch, branchAheadOfRemote, branchNotAheadOfRemote]);
        initialStatuses[branchAheadOfRemote].Ahead.Should().BeGreaterThan(0, "branch should be ahead of remote");
        initialStatuses[branchNotAheadOfRemote].Ahead.Should().Be(0, "branch should not be ahead");
        initialStatuses[sourceBranch].Ahead.Should().Be(0, "source branch should not be ahead");

        // Act
        stackActions.PushChanges(stack, 5, false);

        // Assert - Only the branch that was ahead should be pushed
        var finalStatuses = gitClient.GetBranchStatuses([sourceBranch, branchAheadOfRemote, branchNotAheadOfRemote]);
        finalStatuses[branchAheadOfRemote].Ahead.Should().Be(0, "ahead branch should be pushed and synchronized");
        finalStatuses[branchNotAheadOfRemote].Ahead.Should().Be(0, "not-ahead branch should remain unchanged");
        finalStatuses[sourceBranch].Ahead.Should().Be(0, "source branch should remain unchanged");
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

        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(b => b.WithName(sourceBranch).PushToRemote())
            .WithBranch(b => b.WithName(branchUpToDate).PushToRemote())
            .WithBranch(b => b.WithName(branchBehind).PushToRemote())
            .Build();

        var logger = XUnitLogger.CreateLogger<StackActions>(testOutputHelper);
        var gitClient = new GitClient(XUnitLogger.CreateLogger<GitClient>(testOutputHelper), repo.ExecutionContext);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        
        // Set up scenario: one branch is behind (can't be pushed), others are up to date
        gitClient.ChangeBranch(sourceBranch);
        repo.CreateCommitOnRemoteTrackingBranch(branchBehind, "Remote commit making branch behind");

        var stack = new TestStackBuilder()
            .WithSourceBranch(sourceBranch)
            .WithBranch(b => b.WithName(branchUpToDate))
            .WithBranch(b => b.WithName(branchBehind))
            .Build();

        var stackActions = new StackActions(gitClient, gitHubClient, logger, console);

        // Verify initial state - no branches should be ahead
        var initialStatuses = gitClient.GetBranchStatuses([sourceBranch, branchUpToDate, branchBehind]);
        initialStatuses[sourceBranch].Ahead.Should().Be(0, "source branch should not be ahead");
        initialStatuses[branchUpToDate].Ahead.Should().Be(0, "up-to-date branch should not be ahead");
        initialStatuses[branchBehind].Ahead.Should().Be(0, "behind branch should not be ahead");

        // Act
        stackActions.PushChanges(stack, maxBatchSize: 5, forceWithLease: false);

        // Assert - No changes should be made since no branches are ahead
        var finalStatuses = gitClient.GetBranchStatuses([sourceBranch, branchUpToDate, branchBehind]);
        finalStatuses[sourceBranch].Ahead.Should().Be(0, "source branch should remain not ahead");
        finalStatuses[branchUpToDate].Ahead.Should().Be(0, "up-to-date branch should remain not ahead");
        finalStatuses[branchBehind].Ahead.Should().Be(0, "behind branch should remain not ahead");
    }
}