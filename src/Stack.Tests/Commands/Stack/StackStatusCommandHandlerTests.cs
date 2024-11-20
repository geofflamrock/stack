using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class StackStatusCommandHandlerTests
{
    [Fact]
    public async Task WhenMultipleBranchesExistInAStack_AndOneHasAPullRequests_ReturnsStatus()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IStackStatusCommandInputProvider>();
        var outputProvider = Substitute.For<IStackStatusCommandOutputProvider>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]);
        var stack2 = new Config.Stack("Stack2", remoteUri, "branch-2", ["branch-4"]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.SelectStack(Arg.Any<List<Config.Stack>>(), Arg.Any<string>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>(), Arg.Any<GitOperationSettings>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetStatusOfRemoteBranch("branch-3", "branch-1", Arg.Any<GitOperationSettings>())
            .Returns((10, 5));

        gitOperations
            .GetStatusOfRemoteBranch("branch-5", "branch-3", Arg.Any<GitOperationSettings>())
            .Returns((1, 0));

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri());

        gitHubOperations
            .GetPullRequest("branch-3", Arg.Any<GitHubOperationSettings>())
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false), GitOperationSettings.Default, GitHubOperationSettings.Default);

        // Assert
        var expectedBranchStatues = new Dictionary<string, BranchStatus>
        {
            { "branch-3", new BranchStatus(true, 10, 5) },
            { "branch-5", new BranchStatus(true, 1, 0) }
        };
        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            { "branch-3", pr }
        };
        response.Statuses.Should().BeEquivalentTo(new Dictionary<Config.Stack, StackStatus>
        {
            {
                stack1, new(expectedBranchStatues, expectedPullRequests)
            }
        });
    }

    [Fact]
    public async Task WhenStackNameIsProvided_DoesNotAskForStack_ReturnsStatus()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IStackStatusCommandInputProvider>();
        var outputProvider = Substitute.For<IStackStatusCommandOutputProvider>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]);
        var stack2 = new Config.Stack("Stack2", remoteUri, "branch-2", ["branch-4"]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>(), Arg.Any<GitOperationSettings>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetStatusOfRemoteBranch("branch-3", "branch-1", Arg.Any<GitOperationSettings>())
            .Returns((10, 5));

        gitOperations
            .GetStatusOfRemoteBranch("branch-5", "branch-3", Arg.Any<GitOperationSettings>())
            .Returns((1, 0));

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri());

        gitHubOperations
            .GetPullRequest("branch-3", Arg.Any<GitHubOperationSettings>())
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs("Stack1", false), GitOperationSettings.Default, GitHubOperationSettings.Default);

        // Assert
        var expectedBranchStatues = new Dictionary<string, BranchStatus>
        {
            { "branch-3", new BranchStatus(true, 10, 5) },
            { "branch-5", new BranchStatus(true, 1, 0) }
        };
        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            { "branch-3", pr }
        };
        response.Statuses.Should().BeEquivalentTo(new Dictionary<Config.Stack, StackStatus>
        {
            {
                stack1, new(expectedBranchStatues, expectedPullRequests)
            }
        });

        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenAllStacksAreRequested_ReturnsStatusOfEachStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IStackStatusCommandInputProvider>();
        var outputProvider = Substitute.For<IStackStatusCommandOutputProvider>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]);
        var stack2 = new Config.Stack("Stack2", remoteUri, "branch-2", ["branch-4"]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>(), Arg.Any<GitOperationSettings>())
            .Returns(["branch-1", "branch-2", "branch-3", "branch-4", "branch-5"]);

        gitOperations
            .GetStatusOfRemoteBranch("branch-3", "branch-1", Arg.Any<GitOperationSettings>())
            .Returns((10, 5));

        gitOperations
            .GetStatusOfRemoteBranch("branch-5", "branch-3", Arg.Any<GitOperationSettings>())
            .Returns((1, 0));

        gitOperations
            .GetStatusOfRemoteBranch("branch-4", "branch-2", Arg.Any<GitOperationSettings>())
            .Returns((3, 1));

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri());

        gitHubOperations
            .GetPullRequest("branch-3", Arg.Any<GitHubOperationSettings>())
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, true), GitOperationSettings.Default, GitHubOperationSettings.Default);

        // Assert
        var expectedBranchStatuesForStack1 = new Dictionary<string, BranchStatus>
        {
            { "branch-3", new BranchStatus(true, 10, 5) },
            { "branch-5", new BranchStatus(true, 1, 0) }
        };
        var expectedPullRequestsForStack1 = new Dictionary<string, GitHubPullRequest>
        {
            { "branch-3", pr }
        };
        var expectedBranchStatuesForStack2 = new Dictionary<string, BranchStatus>
        {
            { "branch-4", new BranchStatus(true, 3, 1) }
        };
        var expectedPullRequestsForStack2 = new Dictionary<string, GitHubPullRequest>();
        response.Statuses.Should().BeEquivalentTo(new Dictionary<Config.Stack, StackStatus>
        {
            {
                stack1, new(expectedBranchStatuesForStack1, expectedPullRequestsForStack1)
            },
            {
                stack2, new(expectedBranchStatuesForStack2, expectedPullRequestsForStack2)
            }
        });
    }

    [Fact]
    public async Task WhenStackNameIsProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IStackStatusCommandInputProvider>();
        var outputProvider = Substitute.For<IStackStatusCommandOutputProvider>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]);
        var stack2 = new Config.Stack("Stack2", remoteUri, "branch-2", ["branch-4"]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var incorrectStackName = Some.Name();
        await handler
            .Invoking(async h => await h.Handle(new StackStatusCommandInputs(incorrectStackName, false), GitOperationSettings.Default, GitHubOperationSettings.Default))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{incorrectStackName}' not found.");
    }

    [Fact]
    public async Task WhenMultipleBranchesExistInAStack_AndOneNoLongerExistsOnTheRemote_ReturnsCorrectStatus()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IStackStatusCommandInputProvider>();
        var outputProvider = Substitute.For<IStackStatusCommandOutputProvider>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]);
        var stack2 = new Config.Stack("Stack2", remoteUri, "branch-2", ["branch-4"]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.SelectStack(Arg.Any<List<Config.Stack>>(), Arg.Any<string>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>(), Arg.Any<GitOperationSettings>())
            .Returns(["branch-1", "branch-5"]);

        gitOperations
            .GetStatusOfRemoteBranch("branch-5", "branch-1", Arg.Any<GitOperationSettings>())
            .Returns((1, 0));

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri());

        gitHubOperations
            .GetPullRequest("branch-5", Arg.Any<GitHubOperationSettings>())
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false), GitOperationSettings.Default, GitHubOperationSettings.Default);

        // Assert
        var expectedBranchStatues = new Dictionary<string, BranchStatus>
        {
            { "branch-3", new BranchStatus(false, 0, 0) },
            { "branch-5", new BranchStatus(true, 1, 0) }
        };
        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            { "branch-5", pr }
        };
        response.Statuses.Should().BeEquivalentTo(new Dictionary<Config.Stack, StackStatus>
        {
            {
                stack1, new(expectedBranchStatues, expectedPullRequests)
            }
        });
    }
}
