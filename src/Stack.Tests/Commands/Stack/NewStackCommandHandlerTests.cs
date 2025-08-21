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

public class NewStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WithAnExistingBranch_TheStackIsCreatedAndTheCurrentBranchIsChanged()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var existingBranch = Some.BranchName();
        var stackName = Some.ShortName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch, existingBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Text(Questions.StackName).Returns(stackName);
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Add);
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(existingBranch);

        // Act
        await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new(stackName, remoteUri, sourceBranch, [new Config.Branch(existingBranch, [])])
        });
        gitClient.Received().ChangeBranch(existingBranch);
    }

    [Fact]
    public async Task WithNoBranch_TheStackIsCreatedAndTheCurrentBranchIsNotChanged()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var existingBranch = Some.BranchName();
        var stackName = Some.ShortName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch, existingBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Text(Questions.StackName).Returns(stackName);
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.None);

        // Act
        await handler.Handle(NewStackCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new(stackName, remoteUri, sourceBranch, [])
        });
        gitClient.DidNotReceive().ChangeBranch(Arg.Any<string>());
    }

    [Fact]
    public async Task WhenStackNameIsProvidedInInputs_TheProviderIsNotAskedForAName_AndTheStackIsCreatedWithTheName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var stackName = Some.ShortName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.None);

        var inputs = new NewStackCommandInputs(stackName, null, null);

        // Act
        await handler.Handle(inputs);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new(stackName, remoteUri, sourceBranch, [])
        });
        inputProvider.DidNotReceive().Text(Questions.StackName);
    }

    [Fact]
    public async Task WhenSourceBranchIsProvidedInInputs_TheProviderIsNotAskedForTheBranch_AndTheStackIsCreatedWithTheSourceBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.None);

        var inputs = new NewStackCommandInputs(null, sourceBranch, null);

        // Act
        await handler.Handle(inputs);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [])
        });
        inputProvider.DidNotReceive().Select(Questions.SelectBranch, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenBranchNameIsProvidedInInputs_TheProviderIsNotAskedForTheBranchName_AndTheStackIsCreatedWithTheBranchName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);

        var inputs = new NewStackCommandInputs(null, null, newBranch);

        // Act
        await handler.Handle(inputs);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [new Config.Branch(newBranch, [])])
        });
        gitClient.Received().CreateNewBranch(newBranch, sourceBranch);
        gitClient.Received().PushNewBranch(newBranch);
        inputProvider.DidNotReceive().Text(Questions.BranchName);
    }

    [Fact]
    public async Task WithANewBranch_TheStackIsCreatedAndTheBranchExistsOnTheRemote()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewStackCommandInputs(null, null, null));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [new Config.Branch(newBranch, [])])
        });
        gitClient.Received().CreateNewBranch(newBranch, sourceBranch);
        gitClient.Received().PushNewBranch(newBranch);
    }

    [Fact]
    public async Task WithANewBranch_AndThePushFails_TheStackIsStillCreatedSuccessfully()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var handler = new NewStackCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient
            .When(gc => gc.PushNewBranch(newBranch))
            .Do(_ => throw new Exception("Failed to push branch"));

        inputProvider.Text(Questions.StackName).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewStackCommandInputs(null, null, null));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [new Config.Branch(newBranch, [])])
        });
        gitClient.Received().CreateNewBranch(newBranch, sourceBranch);
        gitClient.Received().PushNewBranch(newBranch);
    }
}
