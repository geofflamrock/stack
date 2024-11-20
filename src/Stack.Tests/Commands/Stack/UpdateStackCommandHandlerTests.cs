using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;
using Stack.Infrastructure;

namespace Stack.Tests.Commands.Stack;

public class UpdateStackCommandHandlerTests
{
    [Fact]
    public async Task WhenMultipleBranchesExistInAStack_UpdatesAndMergesEachBranchInSequence()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IUpdateStackCommandInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stacks = new List<Config.Stack>([stack1]);
        stackConfig.Load().Returns(stacks);

        inputProvider.SelectStack(Arg.Any<List<Config.Stack>>(), Arg.Any<string>()).Returns("Stack1");
        inputProvider.ConfirmUpdate().Returns(true);

        gitOperations.DoesRemoteBranchExist(Arg.Any<string>(), Arg.Any<GitOperationSettings>()).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false), GitOperationSettings.Default);

        // Assert
        gitOperations.Received().UpdateBranch("branch-1", Arg.Any<GitOperationSettings>());
        gitOperations.Received().UpdateBranch("branch-2", Arg.Any<GitOperationSettings>());
        gitOperations.Received().MergeFromLocalSourceBranch("branch-1", Arg.Any<GitOperationSettings>());
        gitOperations.Received().PushBranch("branch-2", Arg.Any<GitOperationSettings>());

        gitOperations.Received().UpdateBranch("branch-2", Arg.Any<GitOperationSettings>());
        gitOperations.Received().UpdateBranch("branch-3", Arg.Any<GitOperationSettings>());
        gitOperations.Received().MergeFromLocalSourceBranch("branch-2", Arg.Any<GitOperationSettings>());
        gitOperations.Received().PushBranch("branch-3", Arg.Any<GitOperationSettings>());
    }

    [Fact]
    public async Task WhenABrancheInTheStackNoLongerExistsOnTheRemote_SkipsOverUpdatingThatBranch()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IUpdateStackCommandInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stacks = new List<Config.Stack>([stack1]);
        stackConfig.Load().Returns(stacks);

        inputProvider.SelectStack(Arg.Any<List<Config.Stack>>(), Arg.Any<string>()).Returns("Stack1");
        inputProvider.ConfirmUpdate().Returns(true);

        var branchesThatExistInRemote = new List<string>(["branch-1", "branch-3"]);

        gitOperations.DoesRemoteBranchExist(Arg.Is<string>(b => branchesThatExistInRemote.Contains(b)), Arg.Any<GitOperationSettings>()).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false), GitOperationSettings.Default);

        // Assert
        gitOperations.Received().UpdateBranch("branch-1", Arg.Any<GitOperationSettings>());
        gitOperations.Received().UpdateBranch("branch-3", Arg.Any<GitOperationSettings>());
        gitOperations.Received().MergeFromLocalSourceBranch("branch-1", Arg.Any<GitOperationSettings>());
        gitOperations.Received().PushBranch("branch-3", Arg.Any<GitOperationSettings>());

        gitOperations.DidNotReceive().UpdateBranch("branch-2", Arg.Any<GitOperationSettings>());
    }

    [Fact]
    public async Task WhenNameIsProvided_DoesNotAskForName_UpdatesCorrectStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IUpdateStackCommandInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stacks = new List<Config.Stack>([stack1]);
        stackConfig.Load().Returns(stacks);

        inputProvider.ConfirmUpdate().Returns(true);

        gitOperations.DoesRemoteBranchExist(Arg.Any<string>(), Arg.Any<GitOperationSettings>()).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs("Stack1", false), GitOperationSettings.Default);

        // Assert
        gitOperations.Received().UpdateBranch("branch-1", Arg.Any<GitOperationSettings>());
        gitOperations.Received().UpdateBranch("branch-2", Arg.Any<GitOperationSettings>());
        gitOperations.Received().MergeFromLocalSourceBranch("branch-1", Arg.Any<GitOperationSettings>());
        gitOperations.Received().PushBranch("branch-2", Arg.Any<GitOperationSettings>());

        gitOperations.Received().UpdateBranch("branch-2", Arg.Any<GitOperationSettings>());
        gitOperations.Received().UpdateBranch("branch-3", Arg.Any<GitOperationSettings>());
        gitOperations.Received().MergeFromLocalSourceBranch("branch-2", Arg.Any<GitOperationSettings>());
        gitOperations.Received().PushBranch("branch-3", Arg.Any<GitOperationSettings>());

        inputProvider.DidNotReceive().SelectStack(Arg.Any<List<Config.Stack>>(), Arg.Any<string>());
    }

    [Fact]
    public async Task WhenForceIsProvided_DoesNotAskForConfirmation()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IUpdateStackCommandInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stacks = new List<Config.Stack>([stack1]);
        stackConfig.Load().Returns(stacks);

        inputProvider.SelectStack(Arg.Any<List<Config.Stack>>(), Arg.Any<string>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, true), GitOperationSettings.Default);

        // Assert
        inputProvider.DidNotReceive().ConfirmUpdate();
    }

    [Fact]
    public async Task WhenNameIsProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IUpdateStackCommandInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-1");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stacks = new List<Config.Stack>([stack1]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new UpdateStackCommandInputs(invalidStackName, false), GitOperationSettings.Default))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenOnASpecificBranchInTheStack_TheSameBranchIsSetAsCurrentAfterTheUpdate()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IUpdateStackCommandInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new UpdateStackCommandHandler(inputProvider, outputProvider, gitOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri(Arg.Any<GitOperationSettings>()).Returns(remoteUri);

        // We are on a specific branch in the stack
        gitOperations.GetCurrentBranch(Arg.Any<GitOperationSettings>()).Returns("branch-2");

        var stack1 = new Config.Stack("Stack1", remoteUri, "branch-1", ["branch-2", "branch-3"]);
        var stacks = new List<Config.Stack>([stack1]);
        stackConfig.Load().Returns(stacks);

        inputProvider.SelectStack(Arg.Any<List<Config.Stack>>(), Arg.Any<string>()).Returns("Stack1");
        inputProvider.ConfirmUpdate().Returns(true);

        gitOperations.DoesRemoteBranchExist(Arg.Any<string>(), Arg.Any<GitOperationSettings>()).Returns(true);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false), GitOperationSettings.Default);

        // Assert
        gitOperations.Received().ChangeBranch("branch-2", Arg.Any<GitOperationSettings>());
    }
}
