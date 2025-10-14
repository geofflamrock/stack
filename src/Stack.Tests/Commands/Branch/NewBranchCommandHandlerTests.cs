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

namespace Stack.Tests.Commands.Branch;

public class NewBranchCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNoInputsProvided_AsksForStackAndBranchAndParentBranch_CreatesAndAddsBranchToStack_PushesToRemote_AndSwitchesToBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var childBranch = Some.BranchName();
        var newBranch = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<NewBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch).WithChildBranch(child => child.WithName(childBranch))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewBranchCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Text(Questions.BranchName, Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(newBranch);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(firstBranch); gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, firstBranch, childBranch });
        gitClient.DoesLocalBranchExist(newBranch).Returns(false);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, null), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Model.Stack>
        {
            new("Stack1", sourceBranch, [new Model.Branch(firstBranch, [new Model.Branch(childBranch, []), new Model.Branch(newBranch, [])])]),
            new("Stack2", sourceBranch, [])
        });
        gitClient.Received().CreateNewBranch(newBranch, firstBranch);
        gitClient.Received().ChangeBranch(newBranch);
    }

    [Fact]
    public async Task WhenStackNameProvided_DoesNotAskForStackName_CreatesAndAddsBranchToStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<NewBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewBranchCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Text(Questions.BranchName, Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(newBranch);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(anotherBranch); gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch });
        gitClient.DoesLocalBranchExist(newBranch).Returns(false);

        // Act
        await handler.Handle(new NewBranchCommandInputs("Stack1", null, null), CancellationToken.None);

        // Assert
        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Model.Stack>
        {
            new("Stack1", sourceBranch, [new Model.Branch(anotherBranch, [new Model.Branch(newBranch, [])])]),
            new("Stack2", sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_CreatesAndAddsBranchToStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<NewBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .Build();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewBranchCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Text(Questions.BranchName, Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(newBranch);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(anotherBranch); gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch });
        gitClient.DoesLocalBranchExist(newBranch).Returns(false);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, null), CancellationToken.None);

        // Assert
        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Model.Stack>
        {
            new("Stack1", sourceBranch, [new Model.Branch(anotherBranch, [new Model.Branch(newBranch, [])])]),
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<NewBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewBranchCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient); gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch });

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new NewBranchCommandInputs(invalidStackName, null, null), CancellationToken.None))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenBranchNameProvided_DoesNotAskForBranchName_CreatesAndAddsBranchToStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<NewBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewBranchCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(anotherBranch); gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch });
        gitClient.DoesLocalBranchExist(newBranch).Returns(false);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, newBranch, null), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Model.Stack>
        {
            new("Stack1", sourceBranch, [new Model.Branch(anotherBranch, [new Model.Branch(newBranch, [])])]),
            new("Stack2", sourceBranch, [])
        });
        await inputProvider.DidNotReceive().Text(Questions.BranchName, Arg.Any<CancellationToken>(), Arg.Any<string>());
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchAlreadyExistLocally_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<NewBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewBranchCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1"); gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch });
        gitClient.DoesLocalBranchExist(anotherBranch).Returns(true);

        // Act and assert
        var invalidBranchName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new NewBranchCommandInputs(null, anotherBranch, null), CancellationToken.None))
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

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<NewBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)
                    .WithChildBranch(child => child.WithName(newBranch))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewBranchCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1"); gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch });
        gitClient.DoesLocalBranchExist(newBranch).Returns(false);

        // Act and assert
        await handler.Invoking(async h => await h.Handle(new NewBranchCommandInputs(null, newBranch, null), CancellationToken.None))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{newBranch}' already exists in stack 'Stack1'.");
    }

    [Fact]
    public async Task WhenPushToTheRemoteFails_StillCreatesTheBranchLocallyAndAddsItToTheStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var newBranch = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<NewBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewBranchCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        gitClient.When(gc => gc.PushNewBranch(newBranch)).Do(_ => { throw new Exception("Failed to push branch"); });

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Text(Questions.BranchName, Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(newBranch);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(anotherBranch); gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch });
        gitClient.DoesLocalBranchExist(newBranch).Returns(false);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, null), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Model.Stack>
        {
            new("Stack1", sourceBranch, [new Model.Branch(anotherBranch, [new Model.Branch(newBranch, [])])]),
            new("Stack2", sourceBranch, [])
        });
        gitClient.Received().CreateNewBranch(newBranch, anotherBranch);
        gitClient.Received().ChangeBranch(newBranch);
    }

    [Fact]
    public async Task WhenParentBranchNotProvided_AsksForParentBranch_CreatesNewBranchUnderneathParent()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var childBranch = Some.BranchName();
        var newBranch = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<NewBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch).WithChildBranch(child => child.WithName(childBranch))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewBranchCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Text(Questions.BranchName, Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(newBranch);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(firstBranch); gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, firstBranch, childBranch });
        gitClient.DoesLocalBranchExist(newBranch).Returns(false);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, null), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Model.Stack>
        {
            new("Stack1", sourceBranch, [new Model.Branch(firstBranch, [new Model.Branch(childBranch, []), new Model.Branch(newBranch, [])])]),
            new("Stack2", sourceBranch, [])
        });
        gitClient.Received().CreateNewBranch(newBranch, firstBranch);
        gitClient.Received().ChangeBranch(newBranch);
    }

    [Fact]
    public async Task WhenParentBranchProvided_DoesNotAskForParentBranch_CreatesNewBranchUnderneathParent()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var childBranch = Some.BranchName();
        var newBranch = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<NewBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(firstBranch).WithChildBranch(child => child.WithName(childBranch))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new NewBranchCommandHandler(inputProvider, logger, displayProvider, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Text(Questions.BranchName, Arg.Any<CancellationToken>(), Arg.Any<string>()).Returns(newBranch); gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, firstBranch, childBranch });
        gitClient.DoesLocalBranchExist(newBranch).Returns(false);

        // Act
        await handler.Handle(new NewBranchCommandInputs(null, null, firstBranch), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Model.Stack>
        {
            new("Stack1", sourceBranch, [new Model.Branch(firstBranch, [new Model.Branch(childBranch, []), new Model.Branch(newBranch, [])])]),
            new("Stack2", sourceBranch, [])
        });
        gitClient.Received().CreateNewBranch(newBranch, firstBranch);
        gitClient.Received().ChangeBranch(newBranch);

        await inputProvider.DidNotReceive().Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }
}
