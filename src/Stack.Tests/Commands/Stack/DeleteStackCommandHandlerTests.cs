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
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act
        var response = await handler.Handle(DeleteStackCommandInputs.Empty);

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", remoteUri, "branch-2", [])
        });
    }

    [Fact]
    public async Task WhenConfirmationIsFalse_DoesNotDeleteStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(false);

        // Act
        var response = await handler.Handle(DeleteStackCommandInputs.Empty);

        // Assert
        response.Should().Be(new DeleteStackCommandResponse(null));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        });
    }

    [Fact]
    public async Task WhenNameIsProvided_AsksForConfirmation_AndDeletesStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act
        var response = await handler.Handle(new DeleteStackCommandInputs("Stack1", false));

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", remoteUri, "branch-2", [])
        });

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenForceIsProvided_DoesNotAskForConfirmation_AndDeletesStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        var response = await handler.Handle(new DeleteStackCommandInputs(null, true));

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", remoteUri, "branch-2", [])
        });

        inputProvider.DidNotReceive().Confirm(Questions.ConfirmDeleteStack);
    }

    [Fact]
    public async Task WhenNameAndForceAreProvided_DoesNotAskForAnyInput_AndDeletesStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        // Act
        var response = await handler.Handle(new DeleteStackCommandInputs("Stack1", true));

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", remoteUri, "branch-2", [])
        });

        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenStackDoesNotExist_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
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
    public async Task WhenThereAreLocalBranchesThatAreNotInTheRemote_AsksToCleanup_AndDeletesThemBeforeDeletingStack()
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
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var handler = new DeleteStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

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
        var response = await handler.Handle(DeleteStackCommandInputs.Empty);

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        gitOperations.GetBranchesThatExistLocally([branchToCleanup, branchToKeep]).Should().BeEquivalentTo([branchToKeep]);
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act
        var response = await handler.Handle(new DeleteStackCommandInputs("Stack1", false));

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEmpty();

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }
}
