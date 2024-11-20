using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
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
        var inputProvider = Substitute.For<IStackSwitchCommandInputProvider>();
        var handler = new StackSwitchCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.SelectBranch(Arg.Any<List<Config.Stack>>(), Arg.Any<string>()).Returns("branch-3");

        // Act
        await handler.Handle(new StackSwitchCommandInputs(null), GitOperationSettings.Default);

        // Assert
        gitOperations.Received().ChangeBranch("branch-3", Arg.Any<GitOperationSettings>());
    }

    [Fact]
    public async Task WhenBranchIsProvided_DoesNotAskForBranch_ChangesToBranch()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IStackSwitchCommandInputProvider>();
        var handler = new StackSwitchCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        // Act
        await handler.Handle(new StackSwitchCommandInputs("branch-3"), GitOperationSettings.Default);

        // Assert
        gitOperations.Received().ChangeBranch("branch-3", Arg.Any<GitOperationSettings>());
        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenBranchIsProvided_AndBranchDoesNotExist_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IStackSwitchCommandInputProvider>();
        var handler = new StackSwitchCommandHandler(inputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-3", Arg.Any<GitOperationSettings>()).Returns(false);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        await handler.Invoking(h => h.Handle(new StackSwitchCommandInputs("branch-3"), GitOperationSettings.Default))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Branch 'branch-3' does not exist.");
    }
}
