using FluentAssertions;
using Meziantou.Extensions.Logging.Xunit;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Remote;

public class PullStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNoStackNameIsProvided_AsksForStack_PullsChangesForTheCorrectStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(child => child.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<PullStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new PullStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient); gitClient.GetCurrentBranch().Returns(branch1);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        // Act
        await handler.Handle(new PullStackCommandInputs(null), CancellationToken.None);

        // Assert
        var expectedStack = stackRepository.Stacks.First(s => s.Name == "Stack1");
        stackActions.Received(1).PullChanges(expectedStack);
        gitClient.Received(1).ChangeBranch(branch1);
    }

    [Fact]
    public async Task WhenNameIsProvided_DoesNotAskForName_PullsChangesForTheCorrectStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

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
        var logger = XUnitLogger.CreateLogger<PullStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new PullStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient); gitClient.GetCurrentBranch().Returns(branch1);

        // Act
        await handler.Handle(new PullStackCommandInputs("Stack1"), CancellationToken.None);

        // Assert
        var expectedStack = stackRepository.Stacks.First(s => s.Name == "Stack1");
        stackActions.Received(1).PullChanges(expectedStack);
        gitClient.Received(1).ChangeBranch(branch1);
        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenNameIsProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

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
        var logger = XUnitLogger.CreateLogger<PullStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new PullStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient); gitClient.GetCurrentBranch().Returns(branch1);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new PullStackCommandInputs(invalidStackName), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }
}
