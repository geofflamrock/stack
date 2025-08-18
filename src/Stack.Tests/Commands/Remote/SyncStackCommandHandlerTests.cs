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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, false, false, false, false));

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs("Stack1", 5, false, false, false, false));

        // Assert
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
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
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new SyncStackCommandInputs(invalidStackName, 5, false, false, false, false)))
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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        // We are on a specific branch in the stack
        gitClient.ChangeBranch(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, false, false, false, false));

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, false, false, false, false));

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, true, false, false, false));

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, false, true, false, false));

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, null, null, false, false));

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, null, null, false, false));

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, true, null, false, false));

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, null, true, false, false));

        // Assert
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteBranch1); // The merge should retain the tip of the branch
    }

    [Fact]
    public async Task WhenNotSpecifyingRebaseOrMerge_AndNoUpdateSettingExists_AndMergeIsSelected_DoesSyncUsingMerge()
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
            .WithConfig("stack.update.strategy", "")
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        var tipOfRemoteBranch1 = repo.GetTipOfRemoteBranch(branch1);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectUpdateStrategy, Arg.Any<UpdateStrategy[]>()).Returns(UpdateStrategy.Merge);
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, null, null, false, false));

        // Assert
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteBranch1); // The merge should retain the tip of the branch
    }

    [Fact]
    public async Task WhenNotSpecifyingRebaseOrMerge_AndNoUpdateSettingsExists_AndRebaseIsSelected_DoesSyncUsingRebase()
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
            .WithConfig("stack.update.strategy", "")
            .Build();

        var tipOfRemoteSourceBranch = repo.GetTipOfRemoteBranch(sourceBranch);
        var tipOfRemoteBranch1 = repo.GetTipOfRemoteBranch(branch1);
        repo.GetCommitsReachableFromBranch(sourceBranch).Should().NotContain(tipOfRemoteSourceBranch);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectUpdateStrategy, Arg.Any<UpdateStrategy[]>()).Returns(UpdateStrategy.Rebase);
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, null, null, false, false));

        // Assert
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().NotContain(tipOfRemoteBranch1); // When doing a rebase we should no longer have the previous tip
    }

    [Fact]
    public async Task WhenBothRebaseAndMergeAreSpecified_AnErrorIsThrown()
    {
        // Arrange
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        // Act and assert
        await handler
            .Invoking(h => h.Handle(new SyncStackCommandInputs(null, 5, true, true, false, false)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot specify both rebase and merge.");
    }

    [Fact]
    public async Task WhenConfirmOptionIsProvided_DoesNotAskForConfirmation()
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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, false, false, true, false));

        // Assert
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        inputProvider.DidNotReceive().Confirm(Questions.ConfirmSyncStack);
    }

    [Fact]
    public async Task WhenNoPushOptionIsProvided_DoesNotPushChangesToRemote()
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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = new StackActions(gitClient, logger);
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.ChangeBranch(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, false, false, false, true));

        // Assert
        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch1).Should().NotContain(tipOfRemoteSourceBranch);
        repo.GetCommitsReachableFromRemoteBranch(branch2).Should().NotContain(tipOfRemoteSourceBranch);
    }
}
