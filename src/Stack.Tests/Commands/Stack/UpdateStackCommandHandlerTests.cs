using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;
using Stack.Infrastructure;
using Stack.Commands.Helpers;

namespace Stack.Tests.Commands.Stack;

public class UpdateStackCommandHandlerTests
{
    [Fact]
    public async Task WhenMultipleBranchesExistInAStack_UpdatesAndMergesEachBranchInSequence()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new global::Stack.Models.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stack2 = new global::Stack.Models.Stack("Stack2", remoteUri, "branch-2", ["branch-4", "branch-5"]);
        var stacks = new List<global::Stack.Models.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmUpdateStack).Returns(true);

        gitOperations.DoesRemoteBranchExist(Arg.Any<string>()).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false));

        // Assert
        gitOperations.Received().UpdateBranch("branch-1");
        gitOperations.Received().UpdateBranch("branch-2");
        gitOperations.Received().MergeFromLocalSourceBranch("branch-1");
        gitOperations.Received().PushBranch("branch-2");

        gitOperations.Received().UpdateBranch("branch-2");
        gitOperations.Received().UpdateBranch("branch-3");
        gitOperations.Received().MergeFromLocalSourceBranch("branch-2");
        gitOperations.Received().PushBranch("branch-3");
    }

    [Fact]
    public async Task WhenABranchInTheStackNoLongerExistsOnTheRemote_SkipsOverUpdatingThatBranch()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new global::Stack.Models.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stack2 = new global::Stack.Models.Stack("Stack2", remoteUri, "branch-2", ["branch-4", "branch-5"]);
        var stacks = new List<global::Stack.Models.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmUpdateStack).Returns(true);

        var branchesThatExistInRemote = new List<string>(["branch-1", "branch-3"]);

        gitOperations.DoesRemoteBranchExist(Arg.Is<string>(b => branchesThatExistInRemote.Contains(b))).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false));

        // Assert
        gitOperations.Received().UpdateBranch("branch-1");
        gitOperations.Received().UpdateBranch("branch-3");
        gitOperations.Received().MergeFromLocalSourceBranch("branch-1");
        gitOperations.Received().PushBranch("branch-3");

        gitOperations.DidNotReceive().UpdateBranch("branch-2");
    }

    [Fact]
    public async Task WhenABranchInTheStackExistsOnTheRemote_ButThePullRequestIsMerged_SkipsOverUpdatingThatBranch()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stack2 = new Config.Stack("Stack2", remoteUri, "branch-2", ["branch-4", "branch-5"]);
        var stacks = new List<Config.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmUpdateStack).Returns(true);

        var branchesThatExistInRemote = new List<string>(["branch-1", "branch-2", "branch-3"]);

        gitOperations.DoesRemoteBranchExist(Arg.Is<string>(b => branchesThatExistInRemote.Contains(b))).Returns(true);
        gitHubOperations.GetPullRequest("branch-2").Returns(new GitHubPullRequest(1, Some.Name(), Some.Name(), GitHubPullRequestStates.Merged, Some.HttpsUri()));

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false));

        // Assert
        gitOperations.Received().UpdateBranch("branch-1");
        gitOperations.Received().UpdateBranch("branch-3");
        gitOperations.Received().MergeFromLocalSourceBranch("branch-1");
        gitOperations.Received().PushBranch("branch-3");

        gitOperations.DidNotReceive().UpdateBranch("branch-2");
    }

    [Fact]
    public async Task WhenNameIsProvided_DoesNotAskForName_UpdatesCorrectStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new global::Stack.Models.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stack2 = new global::Stack.Models.Stack("Stack2", remoteUri, "branch-2", ["branch-4", "branch-5"]);
        var stacks = new List<global::Stack.Models.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Confirm(Questions.ConfirmUpdateStack).Returns(true);

        gitOperations.DoesRemoteBranchExist(Arg.Any<string>()).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs("Stack1", false));

        // Assert
        gitOperations.Received().UpdateBranch("branch-1");
        gitOperations.Received().UpdateBranch("branch-2");
        gitOperations.Received().MergeFromLocalSourceBranch("branch-1");
        gitOperations.Received().PushBranch("branch-2");

        gitOperations.Received().UpdateBranch("branch-2");
        gitOperations.Received().UpdateBranch("branch-3");
        gitOperations.Received().MergeFromLocalSourceBranch("branch-2");
        gitOperations.Received().PushBranch("branch-3");

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenForceIsProvided_DoesNotAskForConfirmation()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new global::Stack.Models.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stack2 = new global::Stack.Models.Stack("Stack2", remoteUri, "branch-2", ["branch-4", "branch-5"]);
        var stacks = new List<global::Stack.Models.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, true));

        // Assert
        inputProvider.DidNotReceive().Confirm(Questions.ConfirmUpdateStack);
    }

    [Fact]
    public async Task WhenNameIsProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new global::Stack.Models.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stack2 = new global::Stack.Models.Stack("Stack2", remoteUri, "branch-2", ["branch-4", "branch-5"]);
        var stacks = new List<global::Stack.Models.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new UpdateStackCommandInputs(invalidStackName, false)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenOnASpecificBranchInTheStack_TheSameBranchIsSetAsCurrentAfterTheUpdate()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);

        // We are on a specific branch in the stack
        gitOperations.GetCurrentBranch().Returns("branch-2");

        var stack1 = new global::Stack.Models.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stack2 = new global::Stack.Models.Stack("Stack2", remoteUri, "branch-2", ["branch-4", "branch-5"]);
        var stacks = new List<global::Stack.Models.Stack>([stack1, stack2]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmUpdateStack).Returns(true);

        gitOperations.DoesRemoteBranchExist(Arg.Any<string>()).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false));

        // Assert
        gitOperations.Received().ChangeBranch("branch-2");
    }

    [Fact]
    public async Task WhenOnlyASingleStackExists_DoesNotAskForStackName_UpdatesStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stack1 = new global::Stack.Models.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stacks = new List<global::Stack.Models.Stack>([stack1]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Confirm(Questions.ConfirmUpdateStack).Returns(true);

        gitOperations.DoesRemoteBranchExist(Arg.Any<string>()).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false));

        // Assert
        gitOperations.Received().UpdateBranch("branch-1");
        gitOperations.Received().UpdateBranch("branch-2");
        gitOperations.Received().MergeFromLocalSourceBranch("branch-1");
        gitOperations.Received().PushBranch("branch-2");

        gitOperations.Received().UpdateBranch("branch-2");
        gitOperations.Received().UpdateBranch("branch-3");
        gitOperations.Received().MergeFromLocalSourceBranch("branch-2");
        gitOperations.Received().PushBranch("branch-3");

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }
}
