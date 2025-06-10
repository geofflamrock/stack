using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Stack;

public class CleanupStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchWithoutRemoteTracking))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToKeep))
                .WithBranch(branch => branch.WithName(anotherBranchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(new CleanupStackCommandInputs("Stack1", false));

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new CleanupStackCommandInputs(invalidStackName, false)))
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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(new CleanupStackCommandInputs(null, false));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenConfirmIsProvided_DoesNotAskForConfirmation_DeletesBranches()
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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new CleanupStackCommandInputs(null, true));

        // Assert
        inputProvider.DidNotReceive().Confirm(Questions.ConfirmDeleteBranches);
        repo.GetBranches().Should().NotContain(b => b.FriendlyName == branchToCleanup);
    }

    [Fact]
    public async Task WhenChildBranchExistsLocally_AndHasBeenDeletedFromTheRemote_BranchIsDeletedLocally()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var parentBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(parentBranch, true)
            .WithBranch(branchToCleanup, true)
            .WithBranch(branchToKeep, true)
            .Build();

        repo.DeleteRemoteTrackingBranch(branchToCleanup);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(parentBranch).WithChildBranch(b => b.WithName(branchToCleanup)))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(CleanupStackCommandInputs.Empty);

        // Assert
        repo.GetBranches().Should().NotContain(b => b.FriendlyName == branchToCleanup);
    }
}
