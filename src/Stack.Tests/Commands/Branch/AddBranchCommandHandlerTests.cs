using FluentAssertions;
using NSubstitute;
using Spectre.Console;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class AddBranchCommandHandlerTests
{
    [Fact]
    public async Task WhenNoInputsProvided_AsksForStackAndBranchAndConfirms_AddsBranchToStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .WithBranch(branchToAdd)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var handler = new AddBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToAdd);

        // Act
        await handler.Handle(AddBranchCommandInputs.Empty);

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch, branchToAdd]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_DoesNotAskForStackName_AddsBranchFromStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new AddBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(true);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns("branch-5");

        // Act
        await handler.Handle(new AddBranchCommandInputs("Stack1", null));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_AddsBranchFromStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new AddBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(true);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns("branch-5");

        // Act
        await handler.Handle(new AddBranchCommandInputs(null, null));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
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
        var handler = new AddBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new AddBranchCommandInputs(invalidStackName, null)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenBranchNameProvided_DoesNotAskForBranchName_AddsBranchFromStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new AddBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(true);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new AddBranchCommandInputs(null, "branch-5"));

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
        inputProvider.DidNotReceive().Select(Questions.SelectBranch, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchDoesNotExistLocally_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new AddBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(false);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act and assert
        var invalidBranchName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new AddBranchCommandInputs(null, invalidBranchName)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{invalidBranchName}' does not exist locally.");
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchAlreadyExistsInStack_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new AddBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(true);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act and assert
        await handler.Invoking(async h => await h.Handle(new AddBranchCommandInputs(null, "branch-5")))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch 'branch-5' already exists in stack 'Stack1'.");
    }

    [Fact]
    public async Task WhenAllInputsProvided_DoesNotAskForAnything_AddsBranchFromStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new AddBranchCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesLocalBranchExist("branch-5").Returns(true);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        // Act
        await handler.Handle(new AddBranchCommandInputs("Stack1", "branch-5"));

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        });
        inputProvider.ReceivedCalls().Should().BeEmpty();
    }
}
