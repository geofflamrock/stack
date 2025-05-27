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
    public async Task WithAnExistingBranch_TheStackIsCreatedAndTheCurrentBranchIsChanged()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var existingBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(existingBranch)
            .Build();

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);


        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Add);
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(existingBranch);

        // Act
        await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(existingBranch, [])])
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

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.ChangeBranch(existingBranch);



        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.None);

        // Act
        await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [])
        });

        gitClient.GetCurrentBranch().Should().Be(existingBranch);
    }

    [Fact]
    public async Task WhenStackNameIsProvidedInInputs_TheProviderIsNotAskedForAName_AndTheStackIsCreatedWithTheName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .Build();

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);


        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.None);

        var inputs = new NewStackCommandInputs("Stack1", null, null);

        // Act
        await handler.Handle(inputs);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
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

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);


        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.None);

        var inputs = new NewStackCommandInputs(null, sourceBranch, null);

        // Act
        await handler.Handle(inputs);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
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

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);


        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        // Note there shouldn't be any more inputs required at all

        var inputs = new NewStackCommandInputs(null, null, newBranch);

        // Act
        await handler.Handle(inputs);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(newBranch, [])])
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

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);


        inputProvider.Text(Questions.StackName).Returns("A stack with multiple words");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName, "a-stack-with-multiple-words-1").Returns(newBranch);

        // Act
        await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("A stack with multiple words", repo.RemoteUri, sourceBranch, [new Config.Branch(newBranch, [])])
        });

        gitClient.GetCurrentBranch().Should().Be(newBranch);
    }

    [Fact]
    public async Task WithANewBranch_TheStackIsCreatedAndTheBranchExistsOnTheRemote()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .Build();

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);


        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewStackCommandInputs(null, null, null));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(newBranch, [])])
        });

        repo.GetBranches().Should().Contain(b => b.FriendlyName == newBranch && b.IsTracking);
    }

    [Fact]
    public async Task WithANewBranch_AndThePushFails_TheStackIsStillCreatedSuccessfully()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .Build();

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = Substitute.ForPartsOf<GitClient>(logger, repo.GitClientSettings);
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient
            .WhenForAnyArgs(gc => gc.PushNewBranch(Arg.Any<string>()))
            .Throw(new Exception("Failed to push branch"));


        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewStackCommandInputs(null, null, null));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(newBranch, [])])
        });

        repo.GetBranches().Should().Contain(b => b.FriendlyName == newBranch && !b.IsTracking);
    }
}
