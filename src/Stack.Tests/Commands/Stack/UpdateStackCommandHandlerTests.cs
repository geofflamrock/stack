using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;
using Stack.Infrastructure;
using Stack.Commands.Helpers;
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
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);

        // Act
        await handler.Handle(new UpdateStackCommandInputs("Stack1", false, true));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge);
    }

    [Fact]
    public async Task WhenNameIsProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new UpdateStackCommandInputs(invalidStackName, false, false)))
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
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, stackConfig, stackActions);

        // We are on a specific branch in the stack
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetRemoteUri().Returns(remoteUri);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false, true));

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
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false, true));

        // Assert
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge);
    }

    [Fact]
    public async Task WhenRebaseIsSpecified_StackIsUpdatedUsingRebase()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, stackConfig, stackActions);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, true, false));

        // Assert
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Rebase);
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToRebase_StackIsUpdatedUsingRebase()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, stackConfig, stackActions);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns(UpdateStrategy.Rebase.ToString().ToLower());

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, null));

        // Assert
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Rebase);
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToRebase_ButMergeIsSpecified_StackIsUpdatedUsingMerge()
    {
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, stackConfig, stackActions);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns(UpdateStrategy.Rebase.ToString().ToLower());

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, true));

        // Assert
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge);
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToMerge_StackIsUpdatedUsingMerge()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, stackConfig, stackActions);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns(UpdateStrategy.Merge.ToString().ToLower());

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, null));

        // Assert
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge);
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToMerge_ButRebaseIsSpecified_StackIsUpdatedUsingRebase()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, stackConfig, stackActions);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns(UpdateStrategy.Merge.ToString().ToLower());

        // Act (rebase specified overrides config)
        await handler.Handle(new UpdateStackCommandInputs(null, true, null));

        // Assert
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Rebase);
    }

    [Fact]
    public async Task WhenGitConfigValueDoesNotExist_AndRebaseIsSelected_StackIsUpdatedUsingRebase()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, stackConfig, stackActions);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectUpdateStrategy, Arg.Any<UpdateStrategy[]>()).Returns(UpdateStrategy.Rebase);
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns((string?)null);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, null));

        // Assert
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Rebase);
    }

    [Fact]
    public async Task WhenGitConfigValueDoesNotExist_AndMergeIsSelected_StackIsUpdatedUsingMerge()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, stackConfig, stackActions);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectUpdateStrategy, Arg.Any<UpdateStrategy[]>()).Returns(UpdateStrategy.Merge);
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetConfigValue("stack.update.strategy").Returns((string?)null);

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, null));

        // Assert
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge);
    }

    [Fact]
    public async Task WhenBothRebaseAndMergeAreSpecified_AnErrorIsThrown()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
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
        var stackActions = Substitute.For<IStackActions>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);

        // Act and assert
        await handler
            .Invoking(h => h.Handle(new UpdateStackCommandInputs(null, true, true)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot specify both rebase and merge.");
    }
}
