using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class DeleteStackCommandHandlerTests
{
    [Fact]
    public async Task WhenNoInputsAreProvided_AsksForName_AndConfirmation_AndDeletesStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder().Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, []),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act
        await handler.Handle(DeleteStackCommandInputs.Empty);

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenConfirmationIsFalse_DoesNotDeleteStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder().Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, []),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(false);

        // Act
        await handler.Handle(DeleteStackCommandInputs.Empty);

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, []),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenNameIsProvided_AsksForConfirmation_AndDeletesStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder().Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, []),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act
        await handler.Handle(new DeleteStackCommandInputs("Stack1", false));

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder().Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, []),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act and assert
        await handler
            .Invoking(h => h.Handle(new DeleteStackCommandInputs(Some.Name(), false)))
            .Should()
            .ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WhenThereAreLocalBranchesThatAreDeletedInTheRemote_AsksToCleanup_AndDeletesThemBeforeDeletingStack()
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
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToCleanup, branchToKeep]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);

        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(DeleteStackCommandInputs.Empty);

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        repo.GetBranches().Should().NotContain(b => b.FriendlyName == branchToCleanup);
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder().Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act
        await handler.Handle(new DeleteStackCommandInputs("Stack1", false));

        // Assert
        stacks.Should().BeEmpty();

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenConfirmIsProvided_DoesNotAskForConfirmation_DeletesStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder().Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, []),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new DeleteStackCommandInputs(null, true));

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });

        inputProvider.DidNotReceive().Confirm(Questions.ConfirmDeleteStack);
    }
}
