using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class RemoveBranchCommandHandlerTests
{
    [Fact]
    public async Task WhenNoInputsProvided_AsksForStackAndBranchAndConfirms_RemovesBranchFromStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new RemoveBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns("branch-3");
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, null, false));

        // Assert
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_DoesNotAskForStackName_RemovesBranchFromStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new RemoveBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns("branch-3");
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs("Stack1", null, false));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new RemoveBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new RemoveBranchCommandInputs(invalidStackName, null, false)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenBranchNameProvided_DoesNotAskForBranchName_RemovesBranchFromStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new RemoveBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, "branch-3", false));

        // Assert
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
        inputProvider.DidNotReceive().Select(Questions.SelectBranch, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchDoesNotExistInStack_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new RemoveBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act and assert
        var invalidBranchName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new RemoveBranchCommandInputs(null, invalidBranchName, false)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{invalidBranchName}' not found in stack 'Stack1'.");
    }

    [Fact]
    public async Task WhenForceProvided_DoesNotAskForConfirmation_RemovesBranchFromStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new RemoveBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns("branch-3");

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, null, true));

        // Assert
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
        inputProvider.DidNotReceive().Confirm(Questions.ConfirmRemoveBranch);
    }

    [Fact]
    public async Task WhenAllInputsProvided_DoesNotAskForAnything_RemovesBranchFromStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new RemoveBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        // Act
        await handler.Handle(new RemoveBranchCommandInputs("Stack1", "branch-3", true));

        // Assert
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new RemoveBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns("branch-3");
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, null, false));

        // Assert
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-5"])
        });

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }
}
