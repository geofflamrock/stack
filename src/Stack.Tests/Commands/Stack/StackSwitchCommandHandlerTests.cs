using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class StackSwitchCommandHandlerTests
{
    [Fact]
    public async Task WhenNoInputsAreProvided_AsksForBranch_ChangesToBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToSwitchTo = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToSwitchTo)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new StackSwitchCommandHandler(inputProvider, gitClient, stackConfig);

        gitClient.ChangeBranch(sourceBranch);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToSwitchTo]),
            new("Stack2", repo.RemoteUri, sourceBranch, [anotherBranch])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.SelectGrouped(Questions.SelectBranch, Arg.Any<ChoiceGroup<string>[]>()).Returns(branchToSwitchTo);

        // Act
        await handler.Handle(new StackSwitchCommandInputs(null));

        // Assert
        gitClient.GetCurrentBranch().Should().Be(branchToSwitchTo);
    }

    [Fact]
    public async Task WhenBranchIsProvided_DoesNotAskForBranch_ChangesToBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToSwitchTo = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToSwitchTo)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new StackSwitchCommandHandler(inputProvider, gitClient, stackConfig);

        gitClient.ChangeBranch(sourceBranch);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToSwitchTo]),
            new("Stack2", repo.RemoteUri, sourceBranch, [anotherBranch])
        ]);
        stackConfig.Load().Returns(stacks);

        // Act
        await handler.Handle(new StackSwitchCommandInputs(branchToSwitchTo));

        // Assert
        gitClient.GetCurrentBranch().Should().Be(branchToSwitchTo);
        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenBranchIsProvided_AndBranchDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToSwitchTo = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToSwitchTo)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new StackSwitchCommandHandler(inputProvider, gitClient, stackConfig);

        gitClient.ChangeBranch(sourceBranch);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToSwitchTo]),
            new("Stack2", repo.RemoteUri, sourceBranch, [anotherBranch])
        ]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var invalidBranchName = Some.BranchName();
        await handler.Invoking(h => h.Handle(new StackSwitchCommandInputs(invalidBranchName)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{invalidBranchName}' does not exist.");
    }
}
