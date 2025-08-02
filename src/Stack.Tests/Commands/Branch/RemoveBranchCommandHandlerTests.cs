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

public class RemoveBranchCommandHandlerTests(ITestOutputHelper testOutputHelper)
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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(RemoveBranchCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new Config.Stack("Stack1", repo.RemoteUri, sourceBranch, []),
            new Config.Stack("Stack2", repo.RemoteUri, sourceBranch, [])
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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs("Stack1", null, false));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, branchToRemove, false));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(RemoveBranchCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
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

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, null, true));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, []),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        });
        inputProvider.DidNotReceive().Confirm(Questions.ConfirmRemoveBranch);
    }

    [Fact]
    public async Task WhenSchemaIsV2_AndChildActionIsMoveChildrenToParent_RemovesBranchAndMovesChildrenToParent()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var childBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToRemove)
            .WithBranch(childBranch)
            .Build();

        var stackConfig = new TestStackConfigBuilder()
            .WithSchemaVersion(SchemaVersion.V2)
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove).WithChildBranch(b => b.WithName(childBranch))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);
        inputProvider.Select(Questions.RemoveBranchChildAction, Arg.Any<RemoveBranchChildAction[]>(), Arg.Any<Func<RemoveBranchChildAction, string>>())
            .Returns(RemoveBranchChildAction.MoveChildrenToParent);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(RemoveBranchCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(childBranch, [])])
        });
    }

    [Fact]
    public async Task WhenSchemaIsV2_AndChildActionIsRemoveChildren_RemovesBranchAndDeletesChildren()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var childBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToRemove)
            .WithBranch(childBranch)
            .Build();

        var stackConfig = new TestStackConfigBuilder()
            .WithSchemaVersion(SchemaVersion.V2)
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove).WithChildBranch(b => b.WithName(childBranch))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);
        inputProvider.Select(Questions.RemoveBranchChildAction, Arg.Any<RemoveBranchChildAction[]>(), Arg.Any<Func<RemoveBranchChildAction, string>>())
            .Returns(RemoveBranchChildAction.RemoveChildren);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(RemoveBranchCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenSchemaIsV1_RemovesBranchAndMovesChildrenToParent()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var childBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToRemove)
            .WithBranch(childBranch)
            .Build();

        var stackConfig = new TestStackConfigBuilder()
            .WithSchemaVersion(SchemaVersion.V1)
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove).WithChildBranch(b => b.WithName(childBranch))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(RemoveBranchCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(childBranch, [])])
        });
    }

    [Fact]
    public async Task WhenSchemaIsV2_AndRemoveChildrenIsProvided_RemovesBranchAndDeletesChildren()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var childBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToRemove)
            .WithBranch(childBranch)
            .Build();

        var stackConfig = new TestStackConfigBuilder()
            .WithSchemaVersion(SchemaVersion.V2)
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove).WithChildBranch(b => b.WithName(childBranch))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);
        inputProvider.Select(Questions.RemoveBranchChildAction, Arg.Any<RemoveBranchChildAction[]>(), Arg.Any<Func<RemoveBranchChildAction, string>>())
            .Returns(RemoveBranchChildAction.RemoveChildren);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, null, false, RemoveBranchChildAction.RemoveChildren));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [])
        });

        inputProvider.DidNotReceive().Select(Questions.RemoveBranchChildAction, Arg.Any<RemoveBranchChildAction[]>(), Arg.Any<Func<RemoveBranchChildAction, string>>());
    }

    [Fact]
    public async Task WhenSchemaIsV2_AndMoveChildrenToParentIsProvided_RemovesBranchAndMovesChildrenToParent()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToRemove = Some.BranchName();
        var childBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch)
            .WithBranch(branchToRemove)
            .WithBranch(childBranch)
            .Build();

        var stackConfig = new TestStackConfigBuilder()
            .WithSchemaVersion(SchemaVersion.V2)
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branchToRemove).WithChildBranch(b => b.WithName(childBranch))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new RemoveBranchCommandHandler(inputProvider, logger, gitClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>()).Returns(branchToRemove);
        inputProvider.Select(Questions.RemoveBranchChildAction, Arg.Any<RemoveBranchChildAction[]>(), Arg.Any<Func<RemoveBranchChildAction, string>>())
            .Returns(RemoveBranchChildAction.MoveChildrenToParent);
        inputProvider.Confirm(Questions.ConfirmRemoveBranch).Returns(true);

        // Act
        await handler.Handle(new RemoveBranchCommandInputs(null, null, false, RemoveBranchChildAction.MoveChildrenToParent));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", repo.RemoteUri, sourceBranch, [new Config.Branch(childBranch, [])])
        });

        inputProvider.DidNotReceive().Select(Questions.RemoveBranchChildAction, Arg.Any<RemoveBranchChildAction[]>(), Arg.Any<Func<RemoveBranchChildAction, string>>());
    }
}
