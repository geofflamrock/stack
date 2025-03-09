using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;
using Stack.Infrastructure;
using Stack.Commands.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Remote;

public class PullStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenChangesExistOnTheRemote_TheyArePulledDownToTheLocalBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommitsOnRemoteTrackingBranchOf(sourceBranch, 5, b => b.PushToRemote())
            .WithNumberOfEmptyCommitsOnRemoteTrackingBranchOf(branch1, 3, b => b.PushToRemote())
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        var tipOfRemoteBranch1 = repo.GetTipOfRemoteBranch(branch1);

        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromBranch(branch1).Should().NotContain(tipOfRemoteBranch1);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new PullStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new PullStackCommandInputs(null));

        // Assert
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfRemoteBranch1);
    }

    [Fact]
    public async Task WhenNameIsProvided_DoesNotAskForName_PullsChangesFromRemoteForBranchesInStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommitsOnRemoteTrackingBranchOf(sourceBranch, 5, b => b.PushToRemote())
            .WithNumberOfEmptyCommitsOnRemoteTrackingBranchOf(branch1, 3, b => b.PushToRemote())
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        var tipOfRemoteBranch1 = repo.GetTipOfRemoteBranch(branch1);

        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromBranch(branch1).Should().NotContain(tipOfRemoteBranch1);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new PullStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        // Act
        await handler.Handle(new PullStackCommandInputs("Stack1"));

        // Assert
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfRemoteBranch1);
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
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
            .WithNumberOfEmptyCommitsOnRemoteTrackingBranchOf(sourceBranch, 5, b => b.PushToRemote())
            .WithNumberOfEmptyCommitsOnRemoteTrackingBranchOf(branch1, 3, b => b.PushToRemote())
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        var tipOfRemoteBranch1 = repo.GetTipOfRemoteBranch(branch1);

        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromBranch(branch1).Should().NotContain(tipOfRemoteBranch1);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new PullStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new PullStackCommandInputs(invalidStackName)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenChangesExistOnTheRemote_ForABranchThatIsNotInTheStack_TheyAreNotPulledDownToTheLocalBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommitsOnRemoteTrackingBranchOf(sourceBranch, 5, b => b.PushToRemote())
            .WithNumberOfEmptyCommitsOnRemoteTrackingBranchOf(branch1, 3, b => b.PushToRemote())
            .WithNumberOfEmptyCommitsOnRemoteTrackingBranchOf(branch2, 3, b => b.PushToRemote())
            .Build();

        var tipOfRemoteBranch2 = repo.GetTipOfRemoteBranch(branch2);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new PullStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, [branch2]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new PullStackCommandInputs(null));

        // Assert
        repo.GetCommitsReachableFromBranch(branch2).Should().NotContain(tipOfRemoteBranch2);
    }

    [Fact]
    public async Task WhenABranchDoesNotExistLocallyOrInTheRemote_ItIsNotPulled()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommitsOnRemoteTrackingBranchOf(sourceBranch, 5, b => b.PushToRemote())
            .WithNumberOfEmptyCommitsOnRemoteTrackingBranchOf(branch2, 3, b => b.PushToRemote())
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        var tipOfRemoteBranch2 = repo.GetTipOfRemoteBranch(branch2);

        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().NotContain(tipOfRemoteBranch2);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new PullStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.ChangeBranch(branch2);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new PullStackCommandInputs(null));

        // Assert
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfRemoteBranch2);
    }
}
