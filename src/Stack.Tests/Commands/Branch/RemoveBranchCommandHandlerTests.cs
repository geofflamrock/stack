using FluentAssertions;
using NSubstitute;
using Meziantou.Extensions.Logging.Xunit;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;
using Xunit.Abstractions;
using Stack.Model;

namespace Stack.Tests.Commands.Branch;

public class RemoveBranchCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNoInputsProvided_AsksForAllInputsAndConfirms_RemovesBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RemoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToRemove);
        inputProvider.Select(Questions.RemoveBranchChildAction, Arg.Any<RemoveBranchChildAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<RemoveBranchChildAction, string>>())
            .Returns(RemoveBranchChildAction.RemoveChildren);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await handler.Handle(RemoveBranchCommandInputs.Empty, CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new Config.Stack(StackName.From("Stack1"), remoteUri, sourceBranch, []),
            new Config.Stack(StackName.From("Stack2"), remoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_DoesNotAskForStackName_RemovesBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RemoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToRemove);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs("Stack1", null, false), CancellationToken.None);

        // Assert
        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new(StackName.From("Stack1"), remoteUri, sourceBranch, []),
            new(StackName.From("Stack2"), remoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RemoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new RemoveBranchCommandInputs(invalidStackName, null, false), CancellationToken.None))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenBranchNameProvided_DoesNotAskForBranchName_RemovesBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RemoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmRemoveBranch, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, branchToRemove, false), CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new(StackName.From("Stack1"), remoteUri, sourceBranch, []),
            new(StackName.From("Stack2"), remoteUri, sourceBranch, [])
        });
        await inputProvider.DidNotReceive().Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchDoesNotExistInStack_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RemoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        // Act and assert
        var invalidBranchName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new RemoveBranchCommandInputs(null, invalidBranchName, false), CancellationToken.None))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{invalidBranchName}' not found in stack 'Stack1'.");
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RemoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToRemove);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await handler.Handle(RemoveBranchCommandInputs.Empty, CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new(StackName.From("Stack1"), remoteUri, sourceBranch, [])
        });

        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenConfirmProvided_DoesNotAskForConfirmation_RemovesBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RemoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToRemove);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, null, true), CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new(StackName.From("Stack1"), remoteUri, sourceBranch, []),
            new(StackName.From("Stack2"), remoteUri, sourceBranch, [])
        });
        await inputProvider.DidNotReceive().Confirm(Questions.ConfirmRemoveBranch, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenChildActionIsMoveChildrenToParent_RemovesBranchAndMovesChildrenToParent()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var childBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove).WithChildBranch(b => b.WithName(childBranch))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RemoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToRemove);
        inputProvider.Select(Questions.RemoveBranchChildAction, Arg.Any<RemoveBranchChildAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<RemoveBranchChildAction, string>>())
            .Returns(RemoveBranchChildAction.MoveChildrenToParent);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await handler.Handle(RemoveBranchCommandInputs.Empty, CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new(StackName.From("Stack1"), remoteUri, sourceBranch, [new Config.Branch(childBranch, [])])
        });
    }

    [Fact]
    public async Task WhenChildActionIsRemoveChildren_RemovesBranchAndDeletesChildren()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var childBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove).WithChildBranch(b => b.WithName(childBranch))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RemoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToRemove);
        inputProvider.Select(Questions.RemoveBranchChildAction, Arg.Any<RemoveBranchChildAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<RemoveBranchChildAction, string>>())
            .Returns(RemoveBranchChildAction.RemoveChildren);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await handler.Handle(RemoveBranchCommandInputs.Empty, CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new(StackName.From("Stack1"), remoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenRemoveChildrenIsProvided_RemovesBranchAndDeletesChildren()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var childBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove).WithChildBranch(b => b.WithName(childBranch))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RemoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToRemove);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, null, false, RemoveBranchChildAction.RemoveChildren), CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new(StackName.From("Stack1"), remoteUri, sourceBranch, [])
        });

        await inputProvider.DidNotReceive().Select(Questions.RemoveBranchChildAction, Arg.Any<RemoveBranchChildAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<RemoveBranchChildAction, string>>());
    }

    [Fact]
    public async Task WhenMoveChildrenToParentIsProvided_RemovesBranchAndMovesChildrenToParent()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var childBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove).WithChildBranch(b => b.WithName(childBranch))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RemoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToRemove);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, null, false, RemoveBranchChildAction.MoveChildrenToParent), CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new(StackName.From("Stack1"), remoteUri, sourceBranch, [new Config.Branch(childBranch, [])])
        });

        await inputProvider.DidNotReceive().Select(Questions.RemoveBranchChildAction, Arg.Any<RemoveBranchChildAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<RemoveBranchChildAction, string>>());
    }
}
