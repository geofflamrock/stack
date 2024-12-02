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

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act
        var response = await handler.Handle(DeleteStackCommandInputs.Empty);

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
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

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(false);

        // Act
        var response = await handler.Handle(DeleteStackCommandInputs.Empty);

        // Assert
        response.Should().Be(new DeleteStackCommandResponse(null));
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
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

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act
        var response = await handler.Handle(new DeleteStackCommandInputs("Stack1", false));

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
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

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        var response = await handler.Handle(new DeleteStackCommandInputs(null, true));

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
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

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        // Act
        var response = await handler.Handle(new DeleteStackCommandInputs("Stack1", true));

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
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

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

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
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.GetBranchesThatExistLocally(Arg.Any<string[]>()).Returns(["branch-1", "branch-3"]);
        gitOperations.GetBranchesThatExistInRemote(Arg.Any<string[]>()).Returns(["branch-1"]);

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", [])
        ]);

        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        var response = await handler.Handle(DeleteStackCommandInputs.Empty);

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack2", remoteUri, "branch-2", [])
        });
        gitOperations.Received().DeleteLocalBranch("branch-3");
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

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act
        var response = await handler.Handle(new DeleteStackCommandInputs("Stack1", false));

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEmpty();

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }
}
