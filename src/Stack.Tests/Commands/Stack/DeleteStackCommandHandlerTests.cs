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

public class DeleteStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNoInputsAreProvided_AsksForName_AndConfirmation_AndDeletesStack()
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
        var logger = XUnitLogger.CreateLogger<DeleteStackCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(ci => CreateBranchStatuses(ci.Arg<string[]>(), sourceBranch));

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await handler.Handle(DeleteStackCommandInputs.Empty, CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", stackRepository.RemoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenConfirmationIsFalse_DoesNotDeleteStack()
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
        var logger = XUnitLogger.CreateLogger<DeleteStackCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(ci => CreateBranchStatuses(ci.Arg<string[]>(), sourceBranch));

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        await handler.Handle(DeleteStackCommandInputs.Empty, CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", stackRepository.RemoteUri, sourceBranch, []),
            new("Stack2", stackRepository.RemoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenNameIsProvided_AsksForConfirmation_AndDeletesStack()
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
        var logger = XUnitLogger.CreateLogger<DeleteStackCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(ci => CreateBranchStatuses(ci.Arg<string[]>(), sourceBranch));

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Confirm(Questions.ConfirmDeleteStack, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await handler.Handle(new DeleteStackCommandInputs("Stack1", false), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", stackRepository.RemoteUri, sourceBranch, [])
        });

        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenStackDoesNotExist_Throws()
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
        var logger = XUnitLogger.CreateLogger<DeleteStackCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(ci => CreateBranchStatuses(ci.Arg<string[]>(), sourceBranch));

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Confirm(Questions.ConfirmDeleteStack, Arg.Any<CancellationToken>()).Returns(true);

        // Act and assert
        await handler
            .Invoking(h => h.Handle(new DeleteStackCommandInputs(Some.Name(), false), CancellationToken.None))
            .Should()
            .ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task WhenThereAreLocalBranchesThatAreDeletedInTheRemote_AsksToCleanup_AndDeletesThemBeforeDeletingStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<DeleteStackCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>())
            .Returns(ci =>
            {
                var branches = ci.Arg<string[]>();
                var dict = new Dictionary<string, GitBranchStatus>();
                foreach (var b in branches.Distinct())
                {
                    if (b == branchToCleanup)
                    {
                        dict[b] = new GitBranchStatus(b, $"origin/{b}", false, false, 0, 0, new Commit("abcdef1", "cleanup"));
                    }
                    else
                    {
                        dict[b] = new GitBranchStatus(b, $"origin/{b}", true, false, 0, 0, new Commit("abcdef2", "keep"));
                    }
                }
                return dict;
            });

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack, Arg.Any<CancellationToken>()).Returns(true);
        inputProvider.Confirm(Questions.ConfirmDeleteBranches, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await handler.Handle(DeleteStackCommandInputs.Empty, CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", stackRepository.RemoteUri, sourceBranch, [])
        });
        gitClient.Received(1).DeleteLocalBranch(branchToCleanup);
        gitClient.DidNotReceive().DeleteLocalBranch(branchToKeep);
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<DeleteStackCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(ci => CreateBranchStatuses(ci.Arg<string[]>(), sourceBranch));

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Confirm(Questions.ConfirmDeleteStack, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        await handler.Handle(new DeleteStackCommandInputs("Stack1", false), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEmpty();

        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenConfirmIsProvided_DoesNotAskForConfirmation_DeletesStack()
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
        var logger = XUnitLogger.CreateLogger<DeleteStackCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(ci => CreateBranchStatuses(ci.Arg<string[]>(), sourceBranch));

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        // Act
        await handler.Handle(new DeleteStackCommandInputs(null, true), CancellationToken.None);

        // Assert
        stackRepository.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", stackRepository.RemoteUri, sourceBranch, [])
        });

        await inputProvider.DidNotReceive().Confirm(Questions.ConfirmDeleteStack, Arg.Any<CancellationToken>());
    }

    private static Dictionary<string, GitBranchStatus> CreateBranchStatuses(string[] branches, string sourceBranch)
    {
        var dict = new Dictionary<string, GitBranchStatus>();
        foreach (var b in branches.Distinct())
        {
            // Mark all branches (including source) as existing locally and remotely
            dict[b] = new GitBranchStatus(b, $"origin/{b}", true, b == sourceBranch, 0, 0, new Commit("abcdef0", b));
        }
        return dict;
    }
}
