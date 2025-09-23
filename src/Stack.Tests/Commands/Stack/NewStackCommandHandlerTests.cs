using FluentAssertions;
using NSubstitute;
using Meziantou.Extensions.Logging.Xunit;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
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
        var logger = XUnitLogger.CreateLogger<NewStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackConfig);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch, existingBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Text(Questions.StackName, Arg.Any<CancellationToken>()).Returns(stackName);
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Add);
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(existingBranch);

        // Act
        await handler.Handle(NewStackCommandInputs.Empty, CancellationToken.None);

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
        var logger = XUnitLogger.CreateLogger<NewStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackConfig);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch, existingBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Text(Questions.StackName, Arg.Any<CancellationToken>()).Returns(stackName);
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.None);

        // Act
        await handler.Handle(NewStackCommandInputs.Empty, CancellationToken.None);

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
        var logger = XUnitLogger.CreateLogger<NewStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackConfig);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.None);

        var inputs = new NewStackCommandInputs(stackName, null, null);

        // Act
        await handler.Handle(inputs, CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new(stackName, remoteUri, sourceBranch, [])
        });
        await inputProvider.DidNotReceive().Text(Questions.StackName, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenSourceBranchIsProvidedInInputs_TheProviderIsNotAskedForTheBranch_AndTheStackIsCreatedWithTheSourceBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<NewStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackConfig);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Text(Questions.StackName, Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.None);

        var inputs = new NewStackCommandInputs(null, sourceBranch, null);

        // Act
        await handler.Handle(inputs, CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [])
        });
        await inputProvider.DidNotReceive().Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
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
        var logger = XUnitLogger.CreateLogger<NewStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackConfig);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Text(Questions.StackName, Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(sourceBranch);

        var inputs = new NewStackCommandInputs(null, null, newBranch);

        // Act
        await handler.Handle(inputs, CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [new Config.Branch(newBranch, [])])
        });
        gitClient.Received().CreateNewBranch(newBranch, sourceBranch);
        gitClient.Received().PushNewBranch(newBranch);
        await inputProvider.DidNotReceive().Text(Questions.BranchName, Arg.Any<CancellationToken>());
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
        var logger = XUnitLogger.CreateLogger<NewStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackConfig);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Text(Questions.StackName, Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName, Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewStackCommandInputs(null, null, null), CancellationToken.None);

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
        var logger = XUnitLogger.CreateLogger<NewStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackConfig);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns([sourceBranch]);
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient
            .When(gc => gc.PushNewBranch(newBranch))
            .Do(_ => throw new Exception("Failed to push branch"));

        inputProvider.Text(Questions.StackName, Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectSourceBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(sourceBranch);
        inputProvider.Select(Questions.AddOrCreateBranch, Arg.Any<BranchAction[]>(), Arg.Any<CancellationToken>(), Arg.Any<Func<BranchAction, string>>()).Returns(BranchAction.Create);
        inputProvider.Text(Questions.BranchName, Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewStackCommandInputs(null, null, null), CancellationToken.None);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, [new Config.Branch(newBranch, [])])
        });
        gitClient.Received().CreateNewBranch(newBranch, sourceBranch);
        gitClient.Received().PushNewBranch(newBranch);
    }
}
