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

namespace Stack.Tests.Commands.Branch;

public class MoveBranchCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenMovingBranchWithoutChildren_MovesBranchToNewParent()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var secondBranch = Some.BranchName();
        var branchToMove = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<MoveBranchCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch))
                .WithBranch(branch => branch.WithName(secondBranch)
                    .WithChildBranch(child => child.WithName(branchToMove))))
            .Build();

        var handler = new MoveBranchCommandHandler(inputProvider, logger, displayProvider, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToMove);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(firstBranch);

        // Act
        await handler.Handle(new MoveBranchCommandInputs(null, null, null, null), CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [
                new Config.Branch(firstBranch, [new Config.Branch(branchToMove, [])]),
                new Config.Branch(secondBranch, [])
            ])
        });
    }

    [Fact]
    public async Task WhenMovingBranchWithChildren_AndMoveChildrenOption_MovesBranchWithChildren()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var branchToMove = Some.BranchName();
        var childBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<MoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch))
                .WithBranch(branch => branch.WithName(branchToMove)
                    .WithChildBranch(child => child.WithName(childBranch))))
            .Build();

        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var handler = new MoveBranchCommandHandler(inputProvider, logger, displayProvider, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToMove);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(firstBranch);
        inputProvider.Select(Questions.MoveBranchChildAction, Arg.Any<MoveBranchChildAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<MoveBranchChildAction, string>>())
            .Returns(MoveBranchChildAction.MoveChildren);

        // Act
        await handler.Handle(new MoveBranchCommandInputs(null, null, null, null), CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [
                new Config.Branch(firstBranch, [new Config.Branch(branchToMove, [new Config.Branch(childBranch, [])])])
            ])
        });
    }

    [Fact]
    public async Task WhenMovingBranchWithChildren_AndReParentChildrenOption_MovesBranchAndReParentsChildren()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var branchToMove = Some.BranchName();
        var childBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<MoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch))
                .WithBranch(branch => branch.WithName(branchToMove)
                    .WithChildBranch(child => child.WithName(childBranch))))
            .Build();

        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var handler = new MoveBranchCommandHandler(inputProvider, logger, displayProvider, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToMove);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(firstBranch);
        inputProvider.Select(Questions.MoveBranchChildAction, Arg.Any<MoveBranchChildAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<MoveBranchChildAction, string>>())
            .Returns(MoveBranchChildAction.ReParentChildren);

        // Act
        await handler.Handle(new MoveBranchCommandInputs(null, null, null, null), CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [
                new Config.Branch(firstBranch, [new Config.Branch(branchToMove, [])]),
                new Config.Branch(childBranch, [])
            ])
        });
    }

    [Fact]
    public async Task WhenMovingBranchToSourceBranch_MovesBranchToRootLevel()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var branchToMove = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<MoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch)
                    .WithChildBranch(child => child.WithName(branchToMove))))
            .Build();

        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var handler = new MoveBranchCommandHandler(inputProvider, logger, displayProvider, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToMove);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(sourceBranch);

        // Act
        await handler.Handle(new MoveBranchCommandInputs(null, null, null, null), CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [
                new Config.Branch(firstBranch, []),
                new Config.Branch(branchToMove, [])
            ])
        });
    }

    [Fact]
    public async Task WhenAllInputsProvided_DoesNotPromptUser()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var branchToMove = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<MoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch))
                .WithBranch(branch => branch.WithName(branchToMove)))
            .Build();

        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var handler = new MoveBranchCommandHandler(inputProvider, logger, displayProvider, gitClient, stackConfig);

        // Act
        await handler.Handle(new MoveBranchCommandInputs("Stack1", branchToMove, firstBranch, MoveBranchChildAction.MoveChildren), CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [
                new Config.Branch(firstBranch, [new Config.Branch(branchToMove, [])])
            ])
        });

        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenBranchNotFound_ThrowsException()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var nonExistentBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<MoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch)))
            .Build();

        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var handler = new MoveBranchCommandHandler(inputProvider, logger, displayProvider, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(nonExistentBranch);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new MoveBranchCommandInputs(null, null, null, null), CancellationToken.None));

        exception.Message.Should().Contain($"Branch '{nonExistentBranch}' not found in stack 'Stack1'");
    }

    [Fact]
    public async Task WhenBranchWithoutChildren_DoesNotPromptForChildAction()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var branchToMove = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<MoveBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch))
                .WithBranch(branch => branch.WithName(branchToMove)))
            .Build();

        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var handler = new MoveBranchCommandHandler(inputProvider, logger, displayProvider, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToMove);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(firstBranch);

        // Act
        await handler.Handle(new MoveBranchCommandInputs(null, null, null, null), CancellationToken.None);

        // Assert
        await inputProvider.DidNotReceive().Select(Questions.MoveBranchChildAction, Arg.Any<MoveBranchChildAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<MoveBranchChildAction, string>>());
    }
}