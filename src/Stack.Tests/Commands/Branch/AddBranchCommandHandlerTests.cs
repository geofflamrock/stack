using FluentAssertions;
using Meziantou.Extensions.Logging.Xunit;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Branch;

public class AddBranchCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNoInputsProvided_AsksForStackAndBranchAndParentBranchAndConfirms_AddsBranchToStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var childBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<AddBranchCommandHandler>(testOutputHelper);

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, firstBranch, childBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
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
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToAdd);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(firstBranch);

        // Act
        await handler.Handle(new AddBranchCommandInputs(null, null, null), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", stackRepository.RemoteUri, sourceBranch, [new Config.Branch(firstBranch, [new Config.Branch(childBranch, []), new Config.Branch(branchToAdd, [])])]),
            new("Stack2", stackRepository.RemoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_DoesNotAskForStackName_AddsBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack.WithName("Stack2").WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<AddBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToAdd);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(anotherBranch);

        // Act
        await handler.Handle(new AddBranchCommandInputs("Stack1", null, null), CancellationToken.None);

        // Assert
        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", stackRepository.RemoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(branchToAdd, [])])]),
            new("Stack2", stackRepository.RemoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_AddsBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<AddBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToAdd);
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(anotherBranch);

        // Act
        await handler.Handle(new AddBranchCommandInputs(null, null, null), CancellationToken.None);

        // Assert
        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", stackRepository.RemoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(branchToAdd, [])])])
        });
    }

    [Fact]
    public async Task WhenStackNameProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack.WithName("Stack2").WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<AddBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new AddBranchCommandInputs(invalidStackName, null, null), CancellationToken.None))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenBranchNameProvided_DoesNotAskForBranchName_AddsBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack.WithName("Stack2").WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<AddBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(anotherBranch);

        // Act
        await handler.Handle(new AddBranchCommandInputs(null, branchToAdd, null), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", stackRepository.RemoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(branchToAdd, [])])]),
            new("Stack2", stackRepository.RemoteUri, sourceBranch, [])
        });
        await inputProvider.DidNotReceive().Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchDoesNotExistLocally_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack.WithName("Stack2").WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<AddBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        // Act and assert
        var invalidBranchName = Some.BranchName();
        gitClient.DoesLocalBranchExist(invalidBranchName).Returns(false);
        await handler.Invoking(async h => await h.Handle(new AddBranchCommandInputs(null, invalidBranchName, null), CancellationToken.None))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{invalidBranchName}' does not exist locally.");
    }

    [Fact]
    public async Task WhenBranchNameProvided_ButBranchAlreadyExistsInStack_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch
                    .WithName(anotherBranch)
                    .WithChildBranch(child => child.WithName(branchToAdd))))
            .WithStack(stack => stack.WithName("Stack2").WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<AddBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        // Act and assert
        await handler.Invoking(async h => await h.Handle(new AddBranchCommandInputs(null, branchToAdd, null), CancellationToken.None))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Branch '{branchToAdd}' already exists in stack 'Stack1'.");
    }

    [Fact]
    public async Task WhenAllInputsProvided_DoesNotAskForAnything_AddsBranchFromStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var anotherBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(anotherBranch)))
            .WithStack(stack => stack.WithName("Stack2").WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<AddBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, anotherBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        // Act
        await handler.Handle(new AddBranchCommandInputs("Stack1", branchToAdd, anotherBranch), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", stackRepository.RemoteUri, sourceBranch, [new Config.Branch(anotherBranch, [new Config.Branch(branchToAdd, [])])]),
            new("Stack2", stackRepository.RemoteUri, sourceBranch, [])
        });
        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenParentBranchProvided_DoesNotAskForParentBranch_CreatesNewBranchUnderneathParent()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var firstBranch = Some.BranchName();
        var childBranch = Some.BranchName();
        var branchToAdd = Some.BranchName();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<AddBranchCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate().Returns(new[] { sourceBranch, firstBranch, childBranch, branchToAdd });
        gitClient.DoesLocalBranchExist(branchToAdd).Returns(true);
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
        var handler = new AddBranchCommandHandler(inputProvider, logger, gitClientFactory, executionContext, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns(branchToAdd);

        // Act
        await handler.Handle(new AddBranchCommandInputs(null, null, firstBranch), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", stackRepository.RemoteUri, sourceBranch, [new Config.Branch(firstBranch, [new Config.Branch(childBranch, []), new Config.Branch(branchToAdd, [])])]),
            new("Stack2", stackRepository.RemoteUri, sourceBranch, [])
        });

        await inputProvider.DidNotReceive().Select(Questions.SelectParentBranch, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }
}
