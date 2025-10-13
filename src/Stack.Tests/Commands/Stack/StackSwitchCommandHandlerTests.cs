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

namespace Stack.Tests.Commands.Stack;

public class StackSwitchCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNoInputsAreProvided_AsksForBranch_ChangesToBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToSwitchTo = Some.BranchName();
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToSwitchTo)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(anotherBranch)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackSwitchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var currentBranch = sourceBranch;
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchesThatExistLocally(Arg.Any<string[]>()).Returns([sourceBranch, anotherBranch, branchToSwitchTo]);
        gitClient.DoesLocalBranchExist(branchToSwitchTo).Returns(true);
        gitClient
            .When(g => g.ChangeBranch(Arg.Any<string>()))
            .Do(ci => currentBranch = ci.Arg<string>());

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new StackSwitchCommandHandler(inputProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider
            .SelectGrouped(Questions.SelectBranch, Arg.Any<ChoiceGroup<string>[]>(), Arg.Any<CancellationToken>())
            .Returns(branchToSwitchTo);

        // Act
        await handler.Handle(new StackSwitchCommandInputs(null), CancellationToken.None);

        // Assert
        currentBranch.Should().Be(branchToSwitchTo);
    }

    [Fact]
    public async Task WhenBranchIsProvided_DoesNotAskForBranch_ChangesToBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToSwitchTo = Some.BranchName();
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToSwitchTo)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(anotherBranch)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackSwitchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var currentBranch = sourceBranch;
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchesThatExistLocally(Arg.Any<string[]>()).Returns([sourceBranch, anotherBranch, branchToSwitchTo]);
        gitClient.DoesLocalBranchExist(branchToSwitchTo).Returns(true);
        gitClient
            .When(g => g.ChangeBranch(Arg.Any<string>()))
            .Do(ci => currentBranch = ci.Arg<string>());

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new StackSwitchCommandHandler(inputProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        // Act
        await handler.Handle(new StackSwitchCommandInputs(branchToSwitchTo), CancellationToken.None);

        // Assert
        currentBranch.Should().Be(branchToSwitchTo);
        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenBranchIsProvided_AndBranchDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToSwitchTo = Some.BranchName();
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToSwitchTo)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(anotherBranch)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackSwitchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var currentBranch = sourceBranch;
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchesThatExistLocally(Arg.Any<string[]>()).Returns([sourceBranch, anotherBranch, branchToSwitchTo]);
        gitClient
            .When(g => g.ChangeBranch(Arg.Any<string>()))
            .Do(ci => currentBranch = ci.Arg<string>());

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new StackSwitchCommandHandler(inputProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        // Act and assert
        var invalidBranchName = Some.BranchName();
        gitClient.DoesLocalBranchExist(invalidBranchName).Returns(false);

        await handler.Invoking(h => h.Handle(new StackSwitchCommandInputs(invalidBranchName), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{invalidBranchName}' does not exist.");
    }
}
