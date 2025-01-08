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

public class SyncStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenChangesExistOnTheSourceBranchOnTheRemote_PullsChanges_UpdatesBranches_AndPushesToRemote()
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
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, false, 5, false, false));

        // Assert
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
    }

    [Fact]
    public async Task WhenNameIsProvided_DoesNotAskForName_SyncsCorrectStack()
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
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs("Stack1", false, 5, false, false));

        // Assert
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenNoConfirmIsProvided_DoesNotAskForConfirmation()
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
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, true, 5, false, false));

        // Assert
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        inputProvider.DidNotReceive().Confirm(Questions.ConfirmSyncStack);
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
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new SyncStackCommandInputs(invalidStackName, false, 5, false, false)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenOnASpecificBranchInTheStack_TheSameBranchIsSetAsCurrentAfterTheSync()
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
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        // We are on a specific branch in the stack
        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, false, 5, false, false));

        // Assert
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        gitClient.GetCurrentBranch().Should().Be(branch1);
    }

    [Fact]
    public async Task WhenOnlyASingleStackExists_DoesNotAskForStackName_SyncsStack()
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
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stacks = new List<Config.Stack>([stack1]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, false, 5, false, false));

        // Assert
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        gitClient.GetCurrentBranch().Should().Be(branch1);

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenUsingRebase_ChangesExistOnTheSourceBranchOnTheRemote_PullsChanges_UpdatesBranches_AndPushesToRemote()
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

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, false, 5, true, false));

        // Assert
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().NotContain(tipOfRemoteBranch1); // When doing a rebase we should no longer have the previous tip
    }

    [Fact]
    public async Task WhenUsingMerge_ChangesExistOnTheSourceBranchOnTheRemote_PullsChanges_UpdatesBranches_AndPushesToRemote()
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

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, false, 5, false, true));

        // Assert
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteBranch1); // The merge should retain the tip of the branch
    }

    [Fact]
    public async Task WhenNotSpecifyingRebaseOrMerge_AndUpdateSettingIsRebase_DoesSyncUsingRebase()
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
            .WithConfig("stack.update.strategy", "rebase")
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        var tipOfRemoteBranch1 = repo.GetTipOfRemoteBranch(branch1);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, false, 5, null, null));

        // Assert
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().NotContain(tipOfRemoteBranch1); // When doing a rebase we should no longer have the previous tip
    }

    [Fact]
    public async Task WhenNotSpecifyingRebaseOrMerge_AndUpdateSettingIsMerge_DoesSyncUsingMerge()
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
            .WithConfig("stack.update.strategy", "merge")
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        var tipOfRemoteBranch1 = repo.GetTipOfRemoteBranch(branch1);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, false, 5, null, null));

        // Assert
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteBranch1); // The merge should retain the tip of the branch
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToMerge_ButRebaseIsSpecified_DoesSyncUsingRebase()
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
            .WithConfig("stack.update.strategy", "merge")
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        var tipOfRemoteBranch1 = repo.GetTipOfRemoteBranch(branch1);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, false, 5, true, null));

        // Assert
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().NotContain(tipOfRemoteBranch1); // When doing a rebase we should no longer have the previous tip
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToRebase_ButMergeIsSpecified_DoesSyncUsingMerge()
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
            .WithConfig("stack.update.strategy", "rebase")
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        var tipOfRemoteBranch1 = repo.GetTipOfRemoteBranch(branch1);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        gitClient.ChangeBranch(branch1);

        var stack1 = new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]);
        var stack2 = new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, []);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, false, 5, null, true));

        // Assert
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteBranch1); // The merge should retain the tip of the branch
    }

    [Fact]
    public async Task WhenBothRebaseAndMergeAreSpecified_AnErrorIsThrown()
    {
        // Arrange
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new SyncStackCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        // Act and assert
        await handler
            .Invoking(h => h.Handle(new SyncStackCommandInputs(null, false, 5, true, true)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot specify both rebase and merge.");
    }
}
