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
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(branch1)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false, true));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { sourceBranch, new BranchDetail { Status = new BranchStatus(true, true, true, true, 0, 0, 0, 0, new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim())) } },
            { branch1, new BranchDetail { Status = new BranchStatus(true, true, true, false, 10, 5, 0, 0, new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim())), PullRequest = pr } },
            { branch2, new BranchDetail { Status = new BranchStatus(true, true, true, false, 1, 0, 0, 0, new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim())) } }
        };
        response.Statuses.Should().BeEquivalentTo(
            new Dictionary<Config.Stack, StackStatus>
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
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(branch1)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs("Stack1", false, true));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { sourceBranch, new BranchDetail { Status = new BranchStatus(true, true, true, true, 0, 0, 0, 0, new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim())) } },
            { branch1, new BranchDetail { Status = new BranchStatus(true, true, true, false, 10, 5, 0, 0, new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim())), PullRequest = pr } },
            { branch2, new BranchDetail { Status = new BranchStatus(true, true, true, false, 1, 0, 0, 0, new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim())) } }
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
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var branch3 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithBranch(builder => builder.WithName(branch3).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(3).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);
        var tipOfBranch3 = repo.GetTipOfBranch(branch3);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, [branch3]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(branch1)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, true, true));

        // Assert
        var expectedBranchDetailsForStack1 = new Dictionary<string, BranchDetail>
        {
            { sourceBranch, new BranchDetail { Status = new BranchStatus(true, true, true, true, 0, 0, 0, 0, new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim())) } },
            { branch1, new BranchDetail { Status = new BranchStatus(true, true, true, false, 10, 5, 0, 0, new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim())), PullRequest = pr } },
            { branch2, new BranchDetail { Status = new BranchStatus(true, true, true, false, 1, 0, 0, 0, new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim())) } }
        };
        var expectedBranchDetailsForStack2 = new Dictionary<string, BranchDetail>
        {
            { sourceBranch, new BranchDetail { Status = new BranchStatus(true, true, true, true, 0, 0, 0, 0, new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim())) } },
            { branch3, new BranchDetail { Status = new BranchStatus(true, true, true, false, 3, 5, 0, 0, new Commit(tipOfBranch3.Sha[..7], tipOfBranch3.Message.Trim())) } }
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
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var branch3 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithBranch(builder => builder.WithName(branch3).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(3).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);
        var tipOfBranch3 = repo.GetTipOfBranch(branch3);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, [branch3]);
        var stack3 = new Config.Stack("Stack2", Some.HttpsUri().ToString(), Some.BranchName(), [Some.BranchName()]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(branch1)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, true, true));

        // Assert
        var expectedBranchDetailsForStack1 = new Dictionary<string, BranchDetail>
        {
            { sourceBranch, new BranchDetail { Status = new BranchStatus(true, true, true, true, 0, 0, 0, 0, new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim())) } },
            { branch1, new BranchDetail { Status = new BranchStatus(true, true, true, false, 10, 5, 0, 0, new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim())), PullRequest = pr } },
            { branch2, new BranchDetail { Status = new BranchStatus(true, true, true, false, 1, 0, 0, 0, new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim())) } }
        };
        var expectedBranchDetailsForStack2 = new Dictionary<string, BranchDetail>
        {
            { sourceBranch, new BranchDetail { Status = new BranchStatus(true, true, true, true, 0, 0, 0, 0, new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim())) } },
            { branch3, new BranchDetail { Status = new BranchStatus(true, true, true, false, 3, 5, 0, 0, new Commit(tipOfBranch3.Sha[..7], tipOfBranch3.Message.Trim())) } }
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
            .Invoking(async h => await h.Handle(new StackStatusCommandInputs(incorrectStackName, false, false)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{incorrectStackName}' not found.");
    }

    [Fact]
    public async Task WhenMultipleBranchesExistInAStack_AndOneNoLongerExistsOnTheRemote_ReturnsCorrectStatus()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = "branch-1";
        var branch2 = "branch-2";
        var branch3 = "branch-3";
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).WithNumberOfEmptyCommits(5).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10))
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, [branch3]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(branch2)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false, true));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { sourceBranch, new BranchDetail { Status = new BranchStatus(true, true, true, false, 0, 0, 0, 0, new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim())) } },
            { branch1, new BranchDetail { Status = new BranchStatus(true, false, false, false, 0, 0, 0, 0, new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim())) } },
            { branch2, new BranchDetail { Status = new BranchStatus(true, true, true, true, 11, 0, 0, 0, new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim())), PullRequest = pr } }
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
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var branch3 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).WithNumberOfEmptyCommits(5).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, [branch3]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(branch2)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false, true));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { sourceBranch, new BranchDetail { Status = new BranchStatus(true, true, true, false, 0, 0, 0, 0, new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim())) } },
            { branch1, new BranchDetail { Status = new BranchStatus(false, false, false, false, 0, 0, 0, 0, null) } },
            { branch2, new BranchDetail { Status = new BranchStatus(true, true, true, true, 1, 0, 0, 0, new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim())), PullRequest = pr } }
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
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new StackStatusCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stacks = new List<Config.Stack>([stack1]);
        stackConfig.Load().Returns(stacks);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubOperations
            .GetPullRequest(branch1)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false, true));

        // Assert
        var expectedBranchDetails = new Dictionary<string, BranchDetail>
        {
            { sourceBranch, new BranchDetail { Status = new BranchStatus(true, true, true, true, 0, 0, 0, 0, new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim())) } },
            { branch1, new BranchDetail { Status = new BranchStatus(true, true, true, false, 10, 5, 0, 0, new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim())), PullRequest = pr } },
            { branch2, new BranchDetail { Status = new BranchStatus(true, true, true, false, 1, 0, 0, 0, new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim())) } }
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
