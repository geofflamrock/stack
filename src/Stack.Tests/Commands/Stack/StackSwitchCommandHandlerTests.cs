using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class StackSwitchCommandHandlerTests
{
    [Fact]
    public async Task WhenNoInputsAreProvided_AsksForBranch_ChangesToBranch()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var handler = new StackSwitchCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.SelectGrouped(Questions.SelectBranch, Arg.Any<ChoiceGroup<string>[]>()).Returns("branch-3");

        // Act
        await handler.Handle(new StackSwitchCommandInputs(null));

        // Assert
        gitOperations.Received().ChangeBranch("branch-3");
    }

    [Fact]
    public async Task WhenBranchIsProvided_DoesNotAskForBranch_ChangesToBranch()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var handler = new StackSwitchCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-3").Returns(true);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        // Act
        await handler.Handle(new StackSwitchCommandInputs("branch-3"));

        // Assert
        gitOperations.Received().ChangeBranch("branch-3");
        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenBranchIsProvided_AndBranchDoesNotExist_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var handler = new StackSwitchCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-3").Returns(false);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        await handler.Invoking(h => h.Handle(new StackSwitchCommandInputs("branch-3")))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Branch 'branch-3' does not exist.");
    }
}
