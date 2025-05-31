using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Branch;

public class NewBranchCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNoInputsProvided_AsksForStackAndBranch_CreatesAndAddsBranchToStack_PushesToRemote_AndSwitchesToBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new NewBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, null));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(newBranch, [])])]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        gitClient.GetCurrentBranch().Should().Be(newBranch);
        repo.GetBranches().Should().Contain(b => b.FriendlyName == newBranch && b.IsTracking);
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

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new NewBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewBranchCommandInputs("Stack1", null, null));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(newBranch, [])])]),
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

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .Build();
        var handler = new NewBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, null));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(newBranch, [])])]),
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

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new NewBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new NewBranchCommandInputs(invalidStackName, null, null)))
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

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new NewBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, newBranch, null));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(newBranch, [])])]),
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

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new NewBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act and assert
        var invalidBranchName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new NewBranchCommandInputs(null, anotherBranch, null)))
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

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)
                    .WithChildBranch(child => child.WithName(newBranch))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new NewBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act and assert
        await handler.Invoking(async h => await h.Handle(new NewBranchCommandInputs(null, newBranch, null)))
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

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("A stack with multiple words")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new NewBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("A stack with multiple words");
        inputProvider.Text(Questions.BranchName, "a-stack-with-multiple-words-2").Returns(newBranch);

        // Act
        await handler.Handle(NewBranchCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("A stack with multiple words", repo.RemoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(newBranch, [])])]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        gitClient.GetCurrentBranch().Should().Be(newBranch);
    }

    [Fact]
    public async Task WhenPushToTheRemoteFails_StillCreatesTheBranchLocallyAndAddsItToTheStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(anotherBranch)
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.ForPartsOf<GitClient>(logger, repo.GitClientSettings);
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new NewBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        gitClient
            .WhenForAnyArgs(gc => gc.PushNewBranch(Arg.Any<string>()))
            .Throw(new Exception("Failed to push branch"));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, null));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(newBranch, [])])]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        repo.GetBranches().Should().Contain(b => b.FriendlyName == newBranch && !b.IsTracking);
    }

    [Fact]
    public async Task WhenV2Schema_AndParentBranchNotProvided_AsksForParentBranch_CreatesNewBranchUnderneathParent()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var childBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(firstBranch)
            .WithBranch(childBranch)
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var stackConfig = new TestStackConfigBuilder()
            .WithSchemaVersion(SchemaVersion.V2)
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch).WithChildBranch(child => child.WithName(childBranch))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new NewBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>()).Returns(firstBranch);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, null));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(firstBranch, [new Config.Branch(childBranch, []), new Config.Branch(newBranch, [])])]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        gitClient.GetCurrentBranch().Should().Be(newBranch);
        repo.GetBranches().Should().Contain(b => b.FriendlyName == newBranch && b.IsTracking);
    }

    [Fact]
    public async Task WhenV2Schema_AndParentBranchProvided_DoesNotAskForParentBranch_CreatesNewBranchUnderneathParent()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var childBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(firstBranch)
            .WithBranch(childBranch)
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var stackConfig = new TestStackConfigBuilder()
            .WithSchemaVersion(SchemaVersion.V2)
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch).WithChildBranch(child => child.WithName(childBranch))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new NewBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, firstBranch));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(firstBranch, [new Config.Branch(childBranch, []), new Config.Branch(newBranch, [])])]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        gitClient.GetCurrentBranch().Should().Be(newBranch);
        repo.GetBranches().Should().Contain(b => b.FriendlyName == newBranch && b.IsTracking);

        inputProvider.DidNotReceive().Select(Questions.SelectParentBranch, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenV1Schema_AndParentBranchProvided_ThrowsException()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var childBranch = Some.BranchName();
        var newBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(firstBranch)
            .WithBranch(childBranch)
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var stackConfig = new TestStackConfigBuilder()
            .WithSchemaVersion(SchemaVersion.V1)
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch).WithChildBranch(child => child.WithName(childBranch))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var handler = new NewBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Text(Questions.BranchName, Arg.Any<string>()).Returns(newBranch);

        // Act and assert
        await handler.Invoking(h => h.Handle(new NewBranchCommandInputs(null, null, firstBranch)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Parent branches are not supported in stacks with schema version v1. Please migrate the stack to v2 format.");
    }
}
