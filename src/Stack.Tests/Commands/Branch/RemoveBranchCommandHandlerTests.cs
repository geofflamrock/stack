using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Branch;

public class RemoveBranchCommandHandlerTests
{
    [Fact]
    public async Task WhenNoInputsProvided_AsksForStackAndBranchAndConfirms_RemovesBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToRemove)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToRemove]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(RemoveBranchCommandInputs.Empty);

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, []),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_DoesNotAskForStackName_RemovesBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToRemove)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToRemove]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs("Stack1", null, false));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, []),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToRemove)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToRemove]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
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
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToRemove)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToRemove]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, branchToRemove, false));

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, []),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        inputProvider.DidNotReceive().Select(Questions.SelectBranch, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchDoesNotExistInStack_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToRemove)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToRemove]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
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
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToRemove)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToRemove])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(RemoveBranchCommandInputs.Empty);

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [])
        });

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenConfirmProvided_DoesNotAskForConfirmation_RemovesBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToRemove)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branchToRemove]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, null, true));

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, []),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        inputProvider.DidNotReceive().Confirm(Questions.ConfirmRemoveBranch);
    }
}
