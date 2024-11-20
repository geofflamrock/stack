using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class DeleteStackCommandHandlerTests
{
    [Fact]
    public async Task WhenNoInputsAreProvided_AsksForName_AndConfirmation_AndDeletesStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IDeleteStackCommandInputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.SelectStack(Arg.Any<List<Config.Stack>>(), Arg.Any<string>()).Returns("Stack1");
        inputProvider.ConfirmDelete().Returns(true);

        // Act
        var response = await handler.Handle(DeleteStackCommandInputs.Empty, GitOperationSettings.Default);

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
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IDeleteStackCommandInputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.SelectStack(Arg.Any<List<Config.Stack>>(), Arg.Any<string>()).Returns("Stack1");
        inputProvider.ConfirmDelete().Returns(false);

        // Act
        var response = await handler.Handle(DeleteStackCommandInputs.Empty, GitOperationSettings.Default);

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
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IDeleteStackCommandInputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.ConfirmDelete().Returns(true);

        // Act
        var response = await handler.Handle(new DeleteStackCommandInputs("Stack1", false), GitOperationSettings.Default);

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", remoteUri, "branch-2", [])
        });

        inputProvider.DidNotReceive().SelectStack(Arg.Any<List<Config.Stack>>(), Arg.Any<string>());
    }

    [Fact]
    public async Task WhenForceIsProvided_DoesNotAskForConfirmation_AndDeletesStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IDeleteStackCommandInputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.SelectStack(Arg.Any<List<Config.Stack>>(), Arg.Any<string>()).Returns("Stack1");

        // Act
        var response = await handler.Handle(new DeleteStackCommandInputs(null, true), GitOperationSettings.Default);

        // Assert
        response.Should().Be(new DeleteStackCommandResponse("Stack1"));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", remoteUri, "branch-2", [])
        });

        inputProvider.DidNotReceive().ConfirmDelete();
    }

    [Fact]
    public async Task WhenNameAndForceAreProvided_DoesNotAskForAnyInput_AndDeletesStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IDeleteStackCommandInputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

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
        var response = await handler.Handle(new DeleteStackCommandInputs("Stack1", true), GitOperationSettings.Default);

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
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IDeleteStackCommandInputProvider>();
        var handler = new DeleteStackCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", []),
            new("Stack2", remoteUri, "branch-2", [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.SelectStack(Arg.Any<List<Config.Stack>>(), Arg.Any<string>()).Returns(Some.Name());
        inputProvider.ConfirmDelete().Returns(true);

        // Act and assert
        await handler
            .Invoking(h => h.Handle(DeleteStackCommandInputs.Empty, GitOperationSettings.Default))
            .Should()
            .ThrowAsync<InvalidOperationException>();
    }
}
