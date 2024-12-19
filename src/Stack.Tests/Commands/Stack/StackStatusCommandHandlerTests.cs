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
        var sourceBranch = Some.BranchName();
        var aBranch = Some.BranchName();
        var aSecondBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(aBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(aSecondBranch).FromSourceBranch(aBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [aBranch, aSecondBranch]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(aBranch)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { aBranch, new BranchDetail { Status = new BranchStatus(true, true, 10, 5), PullRequest = pr } },
            { aSecondBranch, new BranchDetail { Status = new BranchStatus(true, true, 1, 0) } }
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
        var sourceBranch = Some.BranchName();
        var aBranch = Some.BranchName();
        var aSecondBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(aBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(aSecondBranch).FromSourceBranch(aBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [aBranch, aSecondBranch]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(aBranch)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs("Stack1", false));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { aBranch, new BranchDetail { Status = new BranchStatus(true, true, 10, 5), PullRequest = pr } },
            { aSecondBranch, new BranchDetail { Status = new BranchStatus(true, true, 1, 0) } }
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
        var sourceBranch = Some.BranchName();
        var aBranch = Some.BranchName();
        var aSecondBranch = Some.BranchName();
        var aThirdBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(aBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(aSecondBranch).FromSourceBranch(aBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithBranch(builder => builder.WithName(aThirdBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(3).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [aBranch, aSecondBranch]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, [aThirdBranch]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(aBranch)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, true));

        // Assert
        var expectedBranchDetailsForStack1 = new Dictionary<string, BranchDetail>
        {
            { aBranch, new BranchDetail { Status = new BranchStatus(true, true, 10, 5), PullRequest = pr } },
            { aSecondBranch, new BranchDetail { Status = new BranchStatus(true, true, 1, 0) } }
        };
        var expectedBranchDetailsForStack2 = new Dictionary<string, BranchDetail>
        {
            { aThirdBranch, new BranchDetail { Status = new BranchStatus(true, true, 3, 5) } }
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
    public async Task WhenAllStacksAreRequested_WithStacksInMultipleRepositories_ReturnsStatusOfEachStackInTheCorrectRepository()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var aBranch = Some.BranchName();
        var aSecondBranch = Some.BranchName();
        var aThirdBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(aBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(aSecondBranch).FromSourceBranch(aBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithBranch(builder => builder.WithName(aThirdBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(3).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [aBranch, aSecondBranch]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, [aThirdBranch]);
        var stack3 = new Config.Stack("Stack2", Some.HttpsUri().ToString(), Some.BranchName(), [Some.BranchName()]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(aBranch)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, true));

        // Assert
        var expectedBranchDetailsForStack1 = new Dictionary<string, BranchDetail>
        {
            { aBranch, new BranchDetail { Status = new BranchStatus(true, true, 10, 5), PullRequest = pr } },
            { aSecondBranch, new BranchDetail { Status = new BranchStatus(true, true, 1, 0) } }
        };
        var expectedBranchDetailsForStack2 = new Dictionary<string, BranchDetail>
        {
            { aThirdBranch, new BranchDetail { Status = new BranchStatus(true, true, 3, 5) } }
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
        var sourceBranch = Some.BranchName();
        var aBranch = Some.BranchName();
        var aSecondBranch = Some.BranchName();
        var aThirdBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(aBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(aSecondBranch).FromSourceBranch(aBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithBranch(builder => builder.WithName(aThirdBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(3).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [aBranch, aSecondBranch]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, [aThirdBranch]);
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
        var sourceBranch = Some.BranchName();
        var aBranch = Some.BranchName();
        var aSecondBranch = Some.BranchName();
        var aThirdBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).WithNumberOfEmptyCommits(5).PushToRemote())
            .WithBranch(builder => builder.WithName(aBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10))
            .WithBranch(builder => builder.WithName(aSecondBranch).FromSourceBranch(aBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [aBranch, aSecondBranch]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, [aThirdBranch]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(aSecondBranch)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { aBranch, new BranchDetail { Status = new BranchStatus(true, false, 0, 0) } },
            { aSecondBranch, new BranchDetail { Status = new BranchStatus(true, true, 11, 0), PullRequest = pr } } // The 11 commits are the 10 commits from the parent branch and one from this branch
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
        var sourceBranch = Some.BranchName();
        var aBranch = Some.BranchName();
        var aSecondBranch = Some.BranchName();
        var aThirdBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).WithNumberOfEmptyCommits(5).PushToRemote())
            .WithBranch(builder => builder.WithName(aSecondBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [aBranch, aSecondBranch]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, [aThirdBranch]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(aSecondBranch)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { aBranch, new BranchDetail { Status = new BranchStatus(false, false, 0, 0) } },
            { aSecondBranch, new BranchDetail { Status = new BranchStatus(true, true, 1, 0), PullRequest = pr } }
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
        var sourceBranch = Some.BranchName();
        var aBranch = Some.BranchName();
        var aSecondBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(aBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(aSecondBranch).FromSourceBranch(aBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [aBranch, aSecondBranch]);
        var stacks = new List<Config.Stack>([stack1]);
        stackConfig.Load().Returns(stacks);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(aBranch)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { aBranch, new BranchDetail { Status = new BranchStatus(true, true, 10, 5), PullRequest = pr } },
            { aSecondBranch, new BranchDetail { Status = new BranchStatus(true, true, 1, 0) } }
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
