using FluentAssertions;
using NSubstitute;
using Meziantou.Extensions.Logging.Xunit;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Commands.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Remote;

public class PushStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenChangesExistLocally_TheyArePushedToTheRemote()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(branch1);

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<PushStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new PushStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.ChangeBranch(branch1);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        // Act
        await handler.Handle(PushStackCommandInputs.Default, CancellationToken.None);

        // Assert
        stackActions.Received(1).PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, false);
    }

    [Fact]
    public async Task WhenNameIsProvided_DoesNotAskForName_PushesChangesToRemoteForBranchesInStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(branch1);

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<PushStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new PushStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.ChangeBranch(branch1);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        // Act
        await handler.Handle(new PushStackCommandInputs("Stack1", 5, false), CancellationToken.None);

        // Assert
        stackActions.Received(1).PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, false);
        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenNameIsProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(branch1);

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<PushStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new PushStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.ChangeBranch(branch1);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new PushStackCommandInputs(invalidStackName, 5, false), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenNumberOfBranchesIsGreaterThanMaxBatchSize_ChangesAreSuccessfullyPushedToTheRemoteInBatches()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(branch1);

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<PushStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new PushStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.ChangeBranch(branch1);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        // Act
        await handler.Handle(new PushStackCommandInputs(null, 1, false), CancellationToken.None);

        // Assert
        stackActions.Received(1).PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 1, false);
    }

    [Fact]
    public async Task WhenUsingForceWithLease_ChangesAreForcePushedToTheRemote()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(branch1);

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<PushStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new PushStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.ChangeBranch(branch1);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        // Act
        await handler.Handle(new PushStackCommandInputs(null, 5, true), CancellationToken.None);

        // Assert
        stackActions.Received(1).PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, true);
    }
}
