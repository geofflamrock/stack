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

public class UpdateStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNameIsProvided_DoesNotAskForName_UpdatesCorrectStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetCurrentBranch().Returns(branch1);

        // Act
        await handler.Handle(new UpdateStackCommandInputs("Stack1", false, true, false), CancellationToken.None);

        // Assert
        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        await stackActions.Received().UpdateStack(Arg.Is<Model.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenNameIsProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetCurrentBranch().Returns(branch1);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new UpdateStackCommandInputs(invalidStackName, false, false, false), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenOnASpecificBranchInTheStack_TheSameBranchIsSetAsCurrentAfterTheUpdate()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        // We are on a specific branch in the stack
        gitClient.GetCurrentBranch().Returns(branch1);
        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false, true, false), CancellationToken.None);

        // Assert current branch preserved
        gitClient.Received().ChangeBranch(branch1);
    }

    [Fact]
    public async Task WhenOnlyASingleStackExists_DoesNotAskForStackName_UpdatesStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetCurrentBranch().Returns(branch1);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false, true, false), CancellationToken.None);

        // Assert
        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        await stackActions.Received().UpdateStack(Arg.Is<Model.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenRebaseIsSpecified_StackIsUpdatedUsingRebase()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        gitClient.GetCurrentBranch().Returns(branch1);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, true, false, false), CancellationToken.None);

        // Assert
        await stackActions.Received().UpdateStack(Arg.Is<Model.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Rebase, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToRebase_StackIsUpdatedUsingRebase()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns(UpdateStrategy.Rebase.ToString().ToLower());

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, null, false), CancellationToken.None);

        // Assert
        await stackActions.Received().UpdateStack(Arg.Is<Model.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Rebase, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToRebase_ButMergeIsSpecified_StackIsUpdatedUsingMerge()
    {
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns(UpdateStrategy.Rebase.ToString().ToLower());

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, true, false), CancellationToken.None);

        // Assert
        await stackActions.Received().UpdateStack(Arg.Is<Model.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToMerge_StackIsUpdatedUsingMerge()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns(UpdateStrategy.Merge.ToString().ToLower());

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, null, false), CancellationToken.None);

        // Assert
        await stackActions.Received().UpdateStack(Arg.Is<Model.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToMerge_ButRebaseIsSpecified_StackIsUpdatedUsingRebase()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns(UpdateStrategy.Merge.ToString().ToLower());

        // Act (rebase specified overrides config)
        await handler.Handle(new UpdateStackCommandInputs(null, true, null, false), CancellationToken.None);

        // Assert
        await stackActions.Received().UpdateStack(Arg.Is<Model.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Rebase, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenGitConfigValueDoesNotExist_AndRebaseIsSelected_StackIsUpdatedUsingRebase()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectUpdateStrategy, Arg.Any<UpdateStrategy[]>(), Arg.Any<CancellationToken>()).Returns(UpdateStrategy.Rebase);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns((string?)null);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, null, false), CancellationToken.None);

        // Assert
        await stackActions.Received().UpdateStack(Arg.Is<Model.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Rebase, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenGitConfigValueDoesNotExist_AndMergeIsSelected_StackIsUpdatedUsingMerge()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectUpdateStrategy, Arg.Any<UpdateStrategy[]>(), Arg.Any<CancellationToken>()).Returns(UpdateStrategy.Merge);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns((string?)null);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, null, false), CancellationToken.None);

        // Assert
        await stackActions.Received().UpdateStack(Arg.Is<Model.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenCheckPullRequestsIsTrue_StackIsUpdatedWithCheckPullRequestsEnabled()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns((string?)null);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, null, true), CancellationToken.None);

        // Assert
        await stackActions.Received().UpdateStack(Arg.Is<Model.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<CancellationToken>(), true);
    }

    [Fact]
    public async Task WhenCheckPullRequestsIsFalse_StackIsUpdatedWithCheckPullRequestsDisabled()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns((string?)null);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, null, false), CancellationToken.None);

        // Assert
        await stackActions.Received().UpdateStack(Arg.Is<Model.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<CancellationToken>(), false);
    }

    [Fact]
    public async Task WhenBothRebaseAndMergeAreSpecified_AnErrorIsThrown()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<UpdateStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new UpdateStackCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository, stackActions);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        // Act and assert
        await handler
            .Invoking(h => h.Handle(new UpdateStackCommandInputs(null, true, true, false), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot specify both rebase and merge.");
    }
}
