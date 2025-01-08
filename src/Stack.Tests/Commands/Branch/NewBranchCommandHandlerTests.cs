using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Branch;

public class NewBranchCommandHandlerTests
{
    [Fact]
    public async Task WhenNoInputsProvided_AsksForStackAndBranchAndConfirms_CreatesAndAddsBranchToStack_DoesNotPushToRemote_AndSwitchesToBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

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
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(true);

        // Act
        await handler.Handle(NewBranchCommandInputs.Empty);

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch, newBranch]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        gitClient.GetCurrentBranch().Should().Be(newBranch);
        repo.GetBranches().Should().Contain(b => b.FriendlyName == newBranch && !b.IsTracking);
    }

    [Fact]
    public async Task WhenSwitchBranchIsFalse_CreatesAndAddsBranchToStackButDoesNotSwitchToBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

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
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(false);

        // Act
        await handler.Handle(NewBranchCommandInputs.Empty);

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch, newBranch]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        gitClient.GetCurrentBranch().Should().NotBe(newBranch);
    }

    [Fact]
    public async Task WhenStackNameProvided_DoesNotAskForStackName_CreatesAndAddsBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewBranchCommandInputs("Stack1", null, false));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch, newBranch]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_CreatesAndAddsBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, false));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch, newBranch]),
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
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
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

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

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, newBranch, false));

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch, newBranch]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        inputProvider.DidNotReceive().Text(Questions.BranchName);
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchAlreadyExistLocally_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, []),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act and assert
        var invalidBranchName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new NewBranchCommandInputs(null, anotherBranch, false)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{anotherBranch}' already exists locally.");
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchAlreadyExistsInStack_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch, newBranch]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act and assert
        await handler.Invoking(async h => await h.Handle(new NewBranchCommandInputs(null, newBranch, false)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{newBranch}' already exists in stack 'Stack1'.");
    }

    [Fact]
    public async Task WhenStackHasANameWithMultipleWords_SuggestsAGoodDefaultNewBranchName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("A stack with multiple words", repo.RemoteUri, sourceBranch, [anotherBranch]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("A stack with multiple words");
        inputProvider.Text(Questions.BranchName, "a-stack-with-multiple-words-2").Returns(newBranch);
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(true);

        // Act
        await handler.Handle(NewBranchCommandInputs.Empty);

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("A stack with multiple words", repo.RemoteUri, sourceBranch, [anotherBranch, newBranch]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        gitClient.GetCurrentBranch().Should().Be(newBranch);
    }

    [Fact]
    public async Task WhenPushIsProvided_CreatesAndAddsBranchToStack_AndPushesBranchToTheRemote()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

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
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(false);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, true));

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch, newBranch]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        gitClient.GetCurrentBranch().Should().NotBe(newBranch);
        repo.GetBranches().Should().Contain(b => b.FriendlyName == newBranch && b.IsTracking);
    }

    [Fact]
    public async Task WhenAskedWhetherToPushToTheRemote_AndTheAnswerIsYes_PushesTheNewBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewBranchCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

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
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);
        inputProvider.Confirm(Questions.ConfirmPushBranch).Returns(true);
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(false);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, false));

        // Assert
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [anotherBranch, newBranch]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        gitClient.GetCurrentBranch().Should().NotBe(newBranch);
        repo.GetBranches().Should().Contain(b => b.FriendlyName == newBranch && b.IsTracking);
    }
}
