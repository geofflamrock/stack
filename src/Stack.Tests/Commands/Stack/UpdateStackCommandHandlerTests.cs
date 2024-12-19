using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;
using Stack.Infrastructure;
using Stack.Commands.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Stack;

public class TestOutputProvider(ITestOutputHelper testOutputHelper) : IOutputProvider
{
    public void Debug(string message)
    {
        testOutputHelper.WriteLine($"DEBUG: {message}");
    }

    public void Error(string message)
    {
        testOutputHelper.WriteLine($"ERROR: {message}");
    }

    public void Information(string message)
    {
        testOutputHelper.WriteLine($"INFO: {message}");
    }

    public void Rule(string message)
    {
        testOutputHelper.WriteLine($"RULE: {message}");
    }

    public void Status(string message, Action action)
    {
        testOutputHelper.WriteLine($"STATUS: {message}");
        action();
    }

    public void Tree(string header, string[] items)
    {
        testOutputHelper.WriteLine($"TREE: {header}");
        foreach (var item in items)
        {
            testOutputHelper.WriteLine($"  {item}");
        }
    }

    public void Warning(string message)
    {
        testOutputHelper.WriteLine($"WARNING: {message}");
    }
}

public class UpdateStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenMultipleBranchesExistInAStack_UpdatesAndMergesEachBranchInSequence()
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
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmUpdateStack).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false));

        // Assert
        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);
    }

    [Fact]
    public async Task WhenABranchInTheStackNoLongerExistsOnTheRemote_SkipsOverUpdatingThatBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10))
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmUpdateStack).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false));

        // Assert
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
    }

    [Fact]
    public async Task WhenABranchInTheStackExistsOnTheRemote_ButThePullRequestIsMerged_SkipsOverUpdatingThatBranch()
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

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmUpdateStack).Returns(true);

        gitHubOperations.GetPullRequest(branch1).Returns(new GitHubPullRequest(1, Some.Name(), Some.Name(), GitHubPullRequestStates.Merged, Some.HttpsUri(), false));

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false));

        // Assert
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
    }

    [Fact]
    public async Task WhenNameIsProvided_DoesNotAskForName_UpdatesCorrectStack()
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
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Confirm(Questions.ConfirmUpdateStack).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs("Stack1", false));

        // Assert
        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenForceIsProvided_DoesNotAskForConfirmation()
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
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, true));

        // Assert
        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);
        inputProvider.DidNotReceive().Confirm(Questions.ConfirmUpdateStack);
    }

    [Fact]
    public async Task WhenNameIsProvided_ButStackDoesNotExist_Throws()
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
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new UpdateStackCommandInputs(invalidStackName, false)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenOnASpecificBranchInTheStack_TheSameBranchIsSetAsCurrentAfterTheUpdate()
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
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        // We are on a specific branch in the stack
        gitOperations.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmUpdateStack).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false));

        // Assert
        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);
        gitOperations.GetCurrentBranch().Should().Be(branch1);
    }

    [Fact]
    public async Task WhenOnlyASingleStackExists_DoesNotAskForStackName_UpdatesStack()
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
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stacks = new List<Config.Stack>([stack1]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Confirm(Questions.ConfirmUpdateStack).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false));

        // Assert
        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }
}
