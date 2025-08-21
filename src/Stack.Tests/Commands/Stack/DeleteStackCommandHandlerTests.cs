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

public class DeleteStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNoInputsAreProvided_AsksForName_AndConfirmation_AndDeletesStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(ci => CreateBranchStatuses(ci.Arg<string[]>(), sourceBranch));

        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act
        await handler.Handle(DeleteStackCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", remoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenConfirmationIsFalse_DoesNotDeleteStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(ci => CreateBranchStatuses(ci.Arg<string[]>(), sourceBranch));

        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(false);

        // Act
        await handler.Handle(DeleteStackCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack1", remoteUri, sourceBranch, []),
            new("Stack2", remoteUri, sourceBranch, [])
        });
    }

    [Fact]
    public async Task WhenNameIsProvided_AsksForConfirmation_AndDeletesStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(ci => CreateBranchStatuses(ci.Arg<string[]>(), sourceBranch));

        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act
        await handler.Handle(new DeleteStackCommandInputs("Stack1", false));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", remoteUri, sourceBranch, [])
        });

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(ci => CreateBranchStatuses(ci.Arg<string[]>(), sourceBranch));

        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act and assert
        await handler
            .Invoking(h => h.Handle(new DeleteStackCommandInputs(Some.Name(), false)))
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

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
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

        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);
        inputProvider.Confirm(Questions.ConfirmDeleteBranches).Returns(true);

        // Act
        await handler.Handle(DeleteStackCommandInputs.Empty);

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", remoteUri, sourceBranch, [])
        });
        gitClient.Received(1).DeleteLocalBranch(branchToCleanup);
        gitClient.DidNotReceive().DeleteLocalBranch(branchToKeep);
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(ci => CreateBranchStatuses(ci.Arg<string[]>(), sourceBranch));

        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Confirm(Questions.ConfirmDeleteStack).Returns(true);

        // Act
        await handler.Handle(new DeleteStackCommandInputs("Stack1", false));

        // Assert
        stackConfig.Stacks.Should().BeEmpty();

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenConfirmIsProvided_DoesNotAskForConfirmation_DeletesStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(ci => CreateBranchStatuses(ci.Arg<string[]>(), sourceBranch));

        var handler = new DeleteStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new DeleteStackCommandInputs(null, true));

        // Assert
        stackConfig.Stacks.Should().BeEquivalentTo(new List<Config.Stack>
        {
            new("Stack2", remoteUri, sourceBranch, [])
        });

        inputProvider.DidNotReceive().Confirm(Questions.ConfirmDeleteStack);
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
