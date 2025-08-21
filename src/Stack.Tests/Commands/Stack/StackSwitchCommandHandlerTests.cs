using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
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
        var remoteUri = Some.HttpsUri().ToString();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToSwitchTo)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(anotherBranch)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var currentBranch = sourceBranch;
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetBranchesThatExistLocally(Arg.Any<string[]>()).Returns([sourceBranch, anotherBranch, branchToSwitchTo]);
        gitClient.DoesLocalBranchExist(branchToSwitchTo).Returns(true);
        gitClient
            .When(g => g.ChangeBranch(Arg.Any<string>()))
            .Do(ci => currentBranch = ci.Arg<string>());

        var handler = new StackSwitchCommandHandler(inputProvider, gitClient, stackConfig);

        inputProvider
            .SelectGrouped(Questions.SelectBranch, Arg.Any<ChoiceGroup<string>[]>())
            .Returns(branchToSwitchTo);

        // Act
        await handler.Handle(new StackSwitchCommandInputs(null));

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
        var remoteUri = Some.HttpsUri().ToString();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToSwitchTo)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(anotherBranch)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var currentBranch = sourceBranch;
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetBranchesThatExistLocally(Arg.Any<string[]>()).Returns([sourceBranch, anotherBranch, branchToSwitchTo]);
        gitClient.DoesLocalBranchExist(branchToSwitchTo).Returns(true);
        gitClient
            .When(g => g.ChangeBranch(Arg.Any<string>()))
            .Do(ci => currentBranch = ci.Arg<string>());

        var handler = new StackSwitchCommandHandler(inputProvider, gitClient, stackConfig);

        // Act
        await handler.Handle(new StackSwitchCommandInputs(branchToSwitchTo));

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
        var remoteUri = Some.HttpsUri().ToString();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToSwitchTo)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(anotherBranch)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var currentBranch = sourceBranch;
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetBranchesThatExistLocally(Arg.Any<string[]>()).Returns([sourceBranch, anotherBranch, branchToSwitchTo]);
        gitClient
            .When(g => g.ChangeBranch(Arg.Any<string>()))
            .Do(ci => currentBranch = ci.Arg<string>());

        var handler = new StackSwitchCommandHandler(inputProvider, gitClient, stackConfig);

        // Act and assert
        var invalidBranchName = Some.BranchName();
        gitClient.DoesLocalBranchExist(invalidBranchName).Returns(false);

        await handler.Invoking(h => h.Handle(new StackSwitchCommandInputs(invalidBranchName)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{invalidBranchName}' does not exist.");
    }
}
