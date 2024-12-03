using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;
using Stack.Infrastructure;
using Stack.Commands.Helpers;

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
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]);
        var stack2 = new Config.Stack("Stack2", remoteUri, "branch-2", ["branch-4"]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetStatusOfRemoteBranch("branch-3", "branch-1")
            .Returns((10, 5));

        gitOperations
            .GetStatusOfRemoteBranch("branch-5", "branch-3")
            .Returns((1, 0));

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri());

        gitHubOperations
            .GetPullRequest("branch-3")
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { "branch-3", new BranchDetail { Status = new BranchStatus(true, true, 10, 5), PullRequest = pr } },
            { "branch-5", new BranchDetail { Status = new BranchStatus(true, true, 1, 0) } }
        };
        response.Statuses.Should().BeEquivalentTo(new Dictionary<Config.Stack, StackStatus>
        {
            {
                stack1, new(expectedBranchDetails)
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
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]);
        var stack2 = new Config.Stack("Stack2", remoteUri, "branch-2", ["branch-4"]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetStatusOfRemoteBranch("branch-3", "branch-1")
            .Returns((10, 5));

        gitOperations
            .GetStatusOfRemoteBranch("branch-5", "branch-3")
            .Returns((1, 0));

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri());

        gitHubOperations
            .GetPullRequest("branch-3")
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs("Stack1", false));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { "branch-3", new BranchDetail { Status = new BranchStatus(true, true, 10, 5), PullRequest = pr } },
            { "branch-5", new BranchDetail { Status = new BranchStatus(true, true, 1, 0) } }
        };
        response.Statuses.Should().BeEquivalentTo(new Dictionary<Config.Stack, StackStatus>
        {
            {
                stack1, new(expectedBranchDetails)
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
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]);
        var stack2 = new Config.Stack("Stack2", remoteUri, "branch-2", ["branch-4"]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-2", "branch-3", "branch-4", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-2", "branch-3", "branch-4", "branch-5"]);

        gitOperations
            .GetStatusOfRemoteBranch("branch-3", "branch-1")
            .Returns((10, 5));

        gitOperations
            .GetStatusOfRemoteBranch("branch-5", "branch-3")
            .Returns((1, 0));

        gitOperations
            .GetStatusOfRemoteBranch("branch-4", "branch-2")
            .Returns((3, 1));

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri());

        gitHubOperations
            .GetPullRequest("branch-3")
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, true));

        // Assert
        var expectedBranchDetailsForStack1 = new Dictionary<string, BranchDetail>
        {
            { "branch-3", new BranchDetail { Status = new BranchStatus(true, true, 10, 5), PullRequest = pr } },
            { "branch-5", new BranchDetail { Status = new BranchStatus(true, true, 1, 0) } }
        };
        var expectedBranchDetailsForStack2 = new Dictionary<string, BranchDetail>
        {
            { "branch-4", new BranchDetail { Status = new BranchStatus(true, true, 3, 1) } }
        };
        response.Statuses.Should().BeEquivalentTo(new Dictionary<Config.Stack, StackStatus>
        {
            {
                stack1, new(expectedBranchDetailsForStack1)
            },
            {
                stack2, new(expectedBranchDetailsForStack2)
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
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]);
        var stack2 = new Config.Stack("Stack2", remoteUri, "branch-2", ["branch-4"]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var incorrectStackName = Some.Name();
        await handler
            .Invoking(async h => await h.Handle(new StackStatusCommandInputs(incorrectStackName, false)))
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
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]);
        var stack2 = new Config.Stack("Stack2", remoteUri, "branch-2", ["branch-4"]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetStatusOfRemoteBranch("branch-5", "branch-1")
            .Returns((1, 0));

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri());

        gitHubOperations
            .GetPullRequest("branch-5")
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { "branch-3", new BranchDetail { Status = new BranchStatus(true, false, 0, 0) } },
            { "branch-5", new BranchDetail { Status = new BranchStatus(true, true, 1, 0), PullRequest = pr } }
        };
        response.Statuses.Should().BeEquivalentTo(new Dictionary<Config.Stack, StackStatus>
        {
            {
                stack1, new(expectedBranchDetails)
            }
        });
    }

    [Fact]
    public async Task WhenMultipleBranchesExistInAStack_AndOneNoLongerExistsOnTheRemoteAndLocally_ReturnsCorrectStatus()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]);
        var stack2 = new Config.Stack("Stack2", remoteUri, "branch-2", ["branch-4"]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-5"]);

        gitOperations
            .GetStatusOfRemoteBranch("branch-5", "branch-1")
            .Returns((1, 0));

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri());

        gitHubOperations
            .GetPullRequest("branch-5")
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { "branch-3", new BranchDetail { Status = new BranchStatus(false, false, 0, 0) } },
            { "branch-5", new BranchDetail { Status = new BranchStatus(true, true, 1, 0), PullRequest = pr } }
        };
        response.Statuses.Should().BeEquivalentTo(new Dictionary<Config.Stack, StackStatus>
        {
            {
                stack1, new(expectedBranchDetails)
            }
        });
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_ReturnsStatus()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]);
        var stacks = new List<Config.Stack>([stack1]);
        stackConfig.Load().Returns(stacks);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetStatusOfRemoteBranch("branch-3", "branch-1")
            .Returns((10, 5));

        gitOperations
            .GetStatusOfRemoteBranch("branch-5", "branch-3")
            .Returns((1, 0));

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri());

        gitHubOperations
            .GetPullRequest("branch-3")
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { "branch-3", new BranchDetail { Status = new BranchStatus(true, true, 10, 5), PullRequest = pr } },
            { "branch-5", new BranchDetail { Status = new BranchStatus(true, true, 1, 0) } }
        };
        response.Statuses.Should().BeEquivalentTo(new Dictionary<Config.Stack, StackStatus>
        {
            {
                stack1, new(expectedBranchDetails)
            }
        });

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }
}
