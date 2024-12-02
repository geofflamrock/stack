using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class NewBranchCommandHandlerTests
{
    [Fact]
    public async Task WhenNoInputsProvided_AsksForStackAndBranchAndConfirms_CreatesAndAddsBranchToStackAndSwitchesToBranch()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(false);

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns("branch-5");
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(true);

        // Act
        await handler.Handle(NewBranchCommandInputs.Empty);

        // Assert
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
        gitOperations.Received().ChangeBranch("branch-5");
    }

    [Fact]
    public async Task WhenSwitchBranchIsFalse_CreatsAndAddsBranchToStackButDoesNotSwitchToBranch()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(false);

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns("branch-5");
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(false);

        // Act
        await handler.Handle(NewBranchCommandInputs.Empty);

        // Assert
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
        gitOperations.DidNotReceive().ChangeBranch("branch-5");
    }

    [Fact]
    public async Task WhenStackNameProvided_DoesNotAskForStackName_CreatesAndAddsBranchFromStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(false);

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns("branch-5");

        // Act
        await handler.Handle(new NewBranchCommandInputs("Stack1", null, false));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_CreatesAndAddsBranchFromStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(false);

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns("branch-5");

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, false));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"])
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
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

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
        await handler.Invoking(async h => await h.Handle(new NewBranchCommandInputs(invalidStackName, null, false)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenBranchNameProvided_DoesNotAskForBranchName_CreatesAndAddsBranchFromStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(false);

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, "branch-5", false));

        // Assert
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
        inputProvider.DidNotReceive().Text(Questions.BranchName);
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchAlreadyExistLocally_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(true);

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act and assert
        var invalidBranchName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new NewBranchCommandInputs(null, "branch-5", false)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch 'branch-5' already exists locally.");
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchAlreadyExistsInStack_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(false);

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act and assert
        await handler.Invoking(async h => await h.Handle(new NewBranchCommandInputs(null, "branch-5", false)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch 'branch-5' already exists in stack 'Stack1'.");
    }

    [Fact]
    public async Task WhenAllInputsProvided_DoesNotAskForAnything_CreatesAndAddsBranchFromStackAndChangesToBranch()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(false);

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        // Act
        await handler.Handle(new NewBranchCommandInputs("Stack1", "branch-5", true));

        // Assert
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenStackHasANameWithMultipleWords_SuggestsAGoodDefaultNewBranchName()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(false);

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("A stack with multiple words", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("A stack with multiple words");
        inputProvider.Text(Questions.BranchName, "a-stack-with-multiple-words-2").Returns("branch-5");
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(true);

        // Act
        await handler.Handle(NewBranchCommandInputs.Empty);

        // Assert
        stacks.Should().BeEquivalentTo(new List<global::Stack.Models.Stack>
        {
            new("A stack with multiple words", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
        gitOperations.Received().ChangeBranch("branch-5");
    }
}
