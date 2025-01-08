using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class NewStackCommandHandlerTests
{
    [Fact]
    public async Task WithANewBranch_AndSwitchingToTheBranch_TheStackIsCreated_DoesNotPushToRemote_AndTheCurrentBranchIsChanged()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(true);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);
        inputProvider.Confirm(Questions.ConfirmPushBranch).Returns(false);
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(true);

        // Act
        var response = await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", sourceBranch, BranchAction.Create, newBranch));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [newBranch])
        });

        gitClient.GetCurrentBranch().Should().Be(newBranch);
        repo.GetBranches().Should().Contain(b => b.FriendlyName == newBranch && !b.IsTracking);
    }

    [Fact]
    public async Task WithAnExistingBranch_AndSwitchingToTheBranch_TheStackIsCreatedAndTheCurrentBranchIsChanged()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var existingBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(existingBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(true);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Add);
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(existingBranch);
        inputProvider.Confirm(Questions.ConfirmPushBranch).Returns(false);
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(true);

        // Act
        var response = await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", sourceBranch, BranchAction.Add, existingBranch));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [existingBranch])
        });

        gitClient.GetCurrentBranch().Should().Be(existingBranch);
    }

    [Fact]
    public async Task WithNoBranch_TheStackIsCreatedAndTheCurrentBranchIsNotChanged()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var existingBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(existingBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        gitClient.ChangeBranch(existingBranch);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));


        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(false);

        // Act
        var response = await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", sourceBranch, null, null));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [])
        });

        gitClient.GetCurrentBranch().Should().Be(existingBranch);
    }

    [Fact]
    public async Task WithANewBranch_AndNotSwitchingToTheBranch_TheStackIsCreatedAndTheCurrentBranchIsNotChanged()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        gitClient.ChangeBranch(sourceBranch);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));


        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(true);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);
        inputProvider.Confirm(Questions.ConfirmPushBranch).Returns(false);
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(false);

        // Act
        var response = await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", sourceBranch, BranchAction.Create, newBranch));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [newBranch])
        });

        gitClient.GetCurrentBranch().Should().Be(sourceBranch);
    }

    [Fact]
    public async Task WithAnExistingBranch_AndNotSwitchingToTheBranch_TheStackIsCreatedAndTheCurrentBranchIsNotChanged()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var existingBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(existingBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        gitClient.ChangeBranch(sourceBranch);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(true);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Add);
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(existingBranch);
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(false);

        // Act
        var response = await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", sourceBranch, BranchAction.Add, existingBranch));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [existingBranch])
        });

        gitClient.GetCurrentBranch().Should().Be(sourceBranch);
    }

    [Fact]
    public async Task WhenStackNameIsProvidedInInputs_TheProviderIsNotAskedForAName_AndTheStackIsCreatedWithTheName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(false);

        var inputs = new NewStackCommandInputs("Stack1", null, null);

        // Act
        var response = await handler.Handle(inputs);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", sourceBranch, null, null));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [])
        });

        inputProvider.DidNotReceive().Text(Questions.StackName);
    }

    [Fact]
    public async Task WhenSourceBranchIsProvidedInInputs_TheProviderIsNotAskedForTheBranch_AndTheStackIsCreatedWithTheSourceBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(false);

        var inputs = new NewStackCommandInputs(null, sourceBranch, null);

        // Act
        var response = await handler.Handle(inputs);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", sourceBranch, null, null));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [])
        });

        inputProvider.DidNotReceive().Select(Questions.SelectBranch, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenBranchNameIsProvidedInInputs_TheProviderIsNotAskedForTheBranchName_AndTheStackIsCreatedWithTheBranchName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        // Note there shouldn't be any more inputs required at all

        var inputs = new NewStackCommandInputs(null, null, newBranch);

        // Act
        var response = await handler.Handle(inputs);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", sourceBranch, BranchAction.Create, newBranch));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [newBranch])
        });

        inputProvider.Received().Text(Questions.StackName);
        inputProvider.Received().Select(Questions.SelectSourceBranch, Arg.Any<string[]>());
        inputProvider.ClearReceivedCalls();
        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenAStackHasANameWithMultipleWords_SuggestsAGoodDefaultNewBranchName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.StackName).Returns("A stack with multiple words");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(true);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName, "a-stack-with-multiple-words-1").Returns(newBranch);
        inputProvider.Confirm(Questions.ConfirmPushBranch).Returns(false);
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(true);

        // Act
        var response = await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("A stack with multiple words", sourceBranch, BranchAction.Create, newBranch));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("A stack with multiple words", repo.RemoteUri, sourceBranch, [newBranch])
        });

        gitClient.GetCurrentBranch().Should().Be(newBranch);
    }

    [Fact]
    public async Task WithANewBranch_AndAskedToPushToTheRemote_TheStackIsCreatedAndTheBranchExistsOnTheRemote()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, outputProvider, gitClient, stackConfig);

        var stacks = new List<Config.Stack>();
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch).Returns(true);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);
        inputProvider.Confirm(Questions.ConfirmPushBranch).Returns(true);
        inputProvider.Confirm(Questions.ConfirmSwitchToBranch).Returns(false);

        // Act
        var response = await handler.Handle(new NewStackCommandInputs(null, null, null));

        // Assert
        response.Should().BeEquivalentTo(new NewStackCommandResponse("Stack1", sourceBranch, BranchAction.Create, newBranch));
        stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [newBranch])
        });

        repo.GetBranches().Should().Contain(b => b.FriendlyName == newBranch && b.IsTracking);
    }
}
