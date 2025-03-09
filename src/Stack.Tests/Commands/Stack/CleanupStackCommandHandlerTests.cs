using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class CleanupStackCommandHandlerTests
{
    [Fact]
    public async Task WhenBranchExistsLocally_ButHasNotBeenPushedToTheRemote_BranchIsNotDeletedLocally()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchWithoutRemoteTracking = Some.BranchName();
        var branchToKeep = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branchWithoutRemoteTracking, false)
            .WithBranch(branchToKeep, true)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchWithoutRemoteTracking, branchToKeep]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);

        var remoteUri = Some.HttpsUri().ToString();
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(CleanupStackCommandInputs.Empty);

        // Assert
        repo.GetBranches().Should().Contain(b => b.FriendlyName == branchWithoutRemoteTracking);
    }

    [Fact]
    public async Task WhenBranchExistsLocally_AndHasBeenDeletedFromTheRemote_BranchIsDeletedLocally()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branchToCleanup, true)
            .WithBranch(branchToKeep, true)
            .Build();

        repo.DeleteRemoteTrackingBranch(branchToCleanup);

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToCleanup, branchToKeep]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);

        var remoteUri = Some.HttpsUri().ToString();
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(CleanupStackCommandInputs.Empty);

        // Assert
        repo.GetBranches().Should().NotContain(b => b.FriendlyName == branchToCleanup);
    }

    [Fact]
    public async Task WhenBranchExistsLocally_AndInRemote_BranchIsNotDeletedLocally()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToKeep = Some.BranchName();
        var anotherBranchToKeep = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branchToKeep, true)
            .WithBranch(anotherBranchToKeep, true)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToKeep, anotherBranchToKeep]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(CleanupStackCommandInputs.Empty);

        // Assert
        repo.GetBranches().Should().Contain(b => b.FriendlyName == anotherBranchToKeep);
    }

    [Fact]
    public async Task WhenConfirmationIsFalse_DoesNotDeleteAnyBranches()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branchToCleanup, false)
            .WithBranch(branchToKeep, true)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToCleanup, branchToKeep]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(false);

        // Act
        await handler.Handle(CleanupStackCommandInputs.Empty);

        // Assert
        gitClient.GetBranchesThatExistLocally([branchToKeep, branchToCleanup]).Should().BeEquivalentTo([branchToKeep, branchToCleanup]);
    }

    [Fact]
    public async Task WhenStackNameIsProvided_ItIsNotAskedFor()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branchToCleanup, false)
            .WithBranch(branchToKeep, true)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToCleanup, branchToKeep]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(new CleanupStackCommandInputs("Stack1"));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenStackNameIsProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branchToCleanup, false)
            .WithBranch(branchToKeep, true)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToCleanup, branchToKeep]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new CleanupStackCommandInputs(invalidStackName)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenOnlyASingleStackExists_StackIsSelectedAutomatically()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branchToCleanup, false)
            .WithBranch(branchToKeep, true)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToCleanup, branchToKeep]),
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(new CleanupStackCommandInputs(null));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }
}
