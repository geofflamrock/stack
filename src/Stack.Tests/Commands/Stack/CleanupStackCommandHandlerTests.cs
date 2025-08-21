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

public class CleanupStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenBranchExistsLocally_ButHasNotBeenPushedToTheRemote_BranchIsNotDeletedLocally()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchWithoutRemoteTracking = Some.BranchName();
        var branchToKeep = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchWithoutRemoteTracking))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        
        // Setup branch statuses - branch without remote tracking should not be cleaned up
        var branchStatuses = new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc123", "source commit")),
            [branchWithoutRemoteTracking] = new(branchWithoutRemoteTracking, null, false, false, 0, 0, new Commit("def456", "local commit")),
            [branchToKeep] = new(branchToKeep, $"origin/{branchToKeep}", true, false, 0, 0, new Commit("ghi789", "keep commit"))
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatuses);
        
        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(CleanupStackCommandInputs.Empty);

        // Assert - branch without remote tracking should not be deleted
        gitClient.DidNotReceive().DeleteLocalBranch(branchWithoutRemoteTracking);
    }

    [Fact]
    public async Task WhenBranchExistsLocally_AndHasBeenDeletedFromTheRemote_BranchIsDeletedLocally()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        
        // Setup branch statuses - branchToCleanup has been deleted from remote
        var branchStatuses = new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc123", "source commit")),
            [branchToCleanup] = new(branchToCleanup, $"origin/{branchToCleanup}", false, false, 0, 0, new Commit("def456", "cleanup commit")),
            [branchToKeep] = new(branchToKeep, $"origin/{branchToKeep}", true, false, 0, 0, new Commit("ghi789", "keep commit"))
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatuses);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(CleanupStackCommandInputs.Empty);

        // Assert
        gitClient.Received().DeleteLocalBranch(branchToCleanup);
    }

    [Fact]
    public async Task WhenBranchExistsLocally_AndInRemote_BranchIsNotDeletedLocally()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToKeep = Some.BranchName();
        var anotherBranchToKeep = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToKeep))
                .WithBranch(branch => branch.WithName(anotherBranchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        
        // Setup branch statuses - all branches exist in remote
        var branchStatuses = new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc123", "source commit")),
            [branchToKeep] = new(branchToKeep, $"origin/{branchToKeep}", true, false, 0, 0, new Commit("def456", "keep commit")),
            [anotherBranchToKeep] = new(anotherBranchToKeep, $"origin/{anotherBranchToKeep}", true, false, 0, 0, new Commit("ghi789", "another keep commit"))
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatuses);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(CleanupStackCommandInputs.Empty);

        // Assert
        gitClient.DidNotReceive().DeleteLocalBranch(branchToKeep);
        gitClient.DidNotReceive().DeleteLocalBranch(anotherBranchToKeep);
    }

    [Fact]
    public async Task WhenConfirmationIsFalse_DoesNotDeleteAnyBranches()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        
        // Setup branch statuses - branchToCleanup has remote tracking but remote branch was deleted
        var branchStatuses = new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc123", "source commit")),
            [branchToCleanup] = new(branchToCleanup, $"origin/{branchToCleanup}", false, false, 0, 0, new Commit("def456", "cleanup commit")),
            [branchToKeep] = new(branchToKeep, $"origin/{branchToKeep}", true, false, 0, 0, new Commit("ghi789", "keep commit"))
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatuses);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(false);

        // Act
        await handler.Handle(CleanupStackCommandInputs.Empty);

        // Assert
        gitClient.DidNotReceive().DeleteLocalBranch(branchToCleanup);
    }

    [Fact]
    public async Task WhenStackNameIsProvided_ItIsNotAskedFor()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        
        // Setup branch statuses - branchToCleanup has remote tracking but remote branch was deleted
        var branchStatuses = new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc123", "source commit")),
            [branchToCleanup] = new(branchToCleanup, $"origin/{branchToCleanup}", false, false, 0, 0, new Commit("def456", "cleanup commit")),
            [branchToKeep] = new(branchToKeep, $"origin/{branchToKeep}", true, false, 0, 0, new Commit("ghi789", "keep commit"))
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatuses);

        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(new CleanupStackCommandInputs("Stack1", false));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenStackNameIsProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new CleanupStackCommandInputs(invalidStackName, false)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenOnlyASingleStackExists_StackIsSelectedAutomatically()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        
        // Setup branch statuses - branchToCleanup has remote tracking but remote branch was deleted
        var branchStatuses = new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc123", "source commit")),
            [branchToCleanup] = new(branchToCleanup, $"origin/{branchToCleanup}", false, false, 0, 0, new Commit("def456", "cleanup commit")),
            [branchToKeep] = new(branchToKeep, $"origin/{branchToKeep}", true, false, 0, 0, new Commit("ghi789", "keep commit"))
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatuses);

        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(new CleanupStackCommandInputs(null, false));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenConfirmIsProvided_DoesNotAskForConfirmation_DeletesBranches()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branchToCleanup))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        
        // Setup branch statuses - branchToCleanup has been deleted from remote
        var branchStatuses = new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc123", "source commit")),
            [branchToCleanup] = new(branchToCleanup, $"origin/{branchToCleanup}", false, false, 0, 0, new Commit("def456", "cleanup commit")),
            [branchToKeep] = new(branchToKeep, $"origin/{branchToKeep}", true, false, 0, 0, new Commit("ghi789", "keep commit"))
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatuses);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new CleanupStackCommandInputs(null, true));

        // Assert
        inputProvider.DidNotReceive().Confirm(Questions.ConfirmDeleteBranches);
        gitClient.Received().DeleteLocalBranch(branchToCleanup);
    }

    [Fact]
    public async Task WhenChildBranchExistsLocally_AndHasBeenDeletedFromTheRemote_BranchIsDeletedLocally()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var parentBranch = Some.BranchName();
        var branchToCleanup = Some.BranchName();
        var branchToKeep = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(parentBranch).WithChildBranch(b => b.WithName(branchToCleanup)))
                .WithBranch(branch => branch.WithName(branchToKeep)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new CleanupStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        
        // Setup branch statuses - branchToCleanup has been deleted from remote
        var branchStatuses = new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc123", "source commit")),
            [parentBranch] = new(parentBranch, $"origin/{parentBranch}", true, false, 0, 0, new Commit("def456", "parent commit")),
            [branchToCleanup] = new(branchToCleanup, $"origin/{branchToCleanup}", false, false, 0, 0, new Commit("ghi789", "cleanup commit")),
            [branchToKeep] = new(branchToKeep, $"origin/{branchToKeep}", true, false, 0, 0, new Commit("jkl012", "keep commit"))
        };
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(branchStatuses);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(CleanupStackCommandInputs.Empty);

        // Assert
        gitClient.Received().DeleteLocalBranch(branchToCleanup);
    }
}
