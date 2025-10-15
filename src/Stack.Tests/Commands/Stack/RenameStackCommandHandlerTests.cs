using FluentAssertions;
using Meziantou.Extensions.Logging.Xunit;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Stack;

public class RenameStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNoInputsAreProvided_AsksForStackAndNewName_RenamesStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RenameStackCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var handler = new RenameStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Text(Questions.StackName, Arg.Any<CancellationToken>()).Returns("RenamedStack");

        // Act
        await handler.Handle(RenameStackCommandInputs.Empty, CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().HaveCount(2);
        stackRepository.Stacks.Should().Contain(s => s.Name == "RenamedStack");
        stackRepository.Stacks.Should().Contain(s => s.Name == "Stack2");
        stackRepository.Stacks.Should().NotContain(s => s.Name == "Stack1");
    }

    [Fact]
    public async Task WhenStackNameProvided_AsksOnlyForNewName_RenamesStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch))
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RenameStackCommandHandler>(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var gitClient = Substitute.For<IGitClient>();

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var handler = new RenameStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        inputProvider.Text(Questions.StackName, Arg.Any<CancellationToken>()).Returns("RenamedStack");

        // Act
        await handler.Handle(new RenameStackCommandInputs("Stack1", null), CancellationToken.None);

        // Assert
        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        stackRepository.Stacks.Should().HaveCount(1);
        stackRepository.Stacks.Should().Contain(s => s.Name == "RenamedStack");
    }

    [Fact]
    public async Task WhenNewNameProvided_AsksOnlyForStack_RenamesStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch))
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RenameStackCommandHandler>(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var gitClient = Substitute.For<IGitClient>();

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var handler = new RenameStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        // Act
        await handler.Handle(new RenameStackCommandInputs(null, "RenamedStack"), CancellationToken.None);

        // Assert
        await inputProvider.DidNotReceive().Text(Questions.StackName, Arg.Any<CancellationToken>());
        stackRepository.Stacks.Should().HaveCount(1);
        stackRepository.Stacks.Should().Contain(s => s.Name == "RenamedStack");
    }

    [Fact]
    public async Task WhenBothInputsProvided_DoesNotAskForAnything_RenamesStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch))
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RenameStackCommandHandler>(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var gitClient = Substitute.For<IGitClient>();

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var handler = new RenameStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        // Act
        await handler.Handle(new RenameStackCommandInputs("Stack1", "RenamedStack"), CancellationToken.None);

        // Assert
        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        await inputProvider.DidNotReceive().Text(Questions.StackName, Arg.Any<CancellationToken>());
        stackRepository.Stacks.Should().HaveCount(1);
        stackRepository.Stacks.Should().Contain(s => s.Name == "RenamedStack");
    }

    [Fact]
    public async Task WhenStackDoesNotExist_ThrowsException()
    {
        // Arrange
        var sourceBranch = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch))
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RenameStackCommandHandler>(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var gitClient = Substitute.For<IGitClient>();
        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var handler = new RenameStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        // Act & Assert
        var act = async () => await handler.Handle(new RenameStackCommandInputs("NonExistentStack", "NewName"), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Stack not found.");
    }

    [Fact]
    public async Task WhenNewNameAlreadyExists_ThrowsException()
    {
        // Arrange
        var sourceBranch = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RenameStackCommandHandler>(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var gitClient = Substitute.For<IGitClient>();
        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var handler = new RenameStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        // Act & Assert
        var act = async () => await handler.Handle(new RenameStackCommandInputs("Stack1", "Stack2"), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("A stack with the name 'Stack2' already exists for this remote.");
    }

    [Fact]
    public async Task WhenRenamingToSameName_DoesNotThrowException()
    {
        // Arrange
        var sourceBranch = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch))
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<RenameStackCommandHandler>(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var gitClient = Substitute.For<IGitClient>();
        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        var handler = new RenameStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        // Act
        await handler.Handle(new RenameStackCommandInputs("Stack1", "Stack1"), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().HaveCount(1);
        stackRepository.Stacks.Should().Contain(s => s.Name == "Stack1");
    }
}