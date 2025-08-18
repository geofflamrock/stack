using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;
using Stack.Infrastructure;
using Stack.Commands.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Remote;

public class SyncStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNameIsProvided_DoesNotAskForName_SyncsCorrectStack()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);
        inputProvider.Select(Questions.SelectUpdateStrategy, Arg.Any<UpdateStrategy[]>()).Returns(UpdateStrategy.Merge);

        // Act
        await handler.Handle(new SyncStackCommandInputs("Stack1", 5, false, false, false, false));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<StackStatus>());
        stackActions.Received().PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, false);
        gitClient.Received().ChangeBranch(branch1);
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(async h => await h.Handle(new SyncStackCommandInputs(invalidStackName, 5, false, false, false, false)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenOnASpecificBranchInTheStack_TheSameBranchIsSetAsCurrentAfterTheSync()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        // We are on a specific branch in the stack
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);
        inputProvider.Select(Questions.SelectUpdateStrategy, Arg.Any<UpdateStrategy[]>()).Returns(UpdateStrategy.Merge);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, false, false, false, false));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<StackStatus>());
        stackActions.Received().PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, false);
        gitClient.Received().ChangeBranch(branch1);
    }

    [Fact]
    public async Task WhenOnlyASingleStackExists_DoesNotAskForStackName_SyncsStack()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);
        inputProvider.Select(Questions.SelectUpdateStrategy, Arg.Any<UpdateStrategy[]>()).Returns(UpdateStrategy.Merge);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, false, false, false, false));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<StackStatus>());
        stackActions.Received().PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, false);
        gitClient.Received().ChangeBranch(branch1);

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenRebaseIsProvided_SyncsStackUsingRebase()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, true, false, false, false));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Rebase, Arg.Any<StackStatus>());
        stackActions.Received().PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, true);
        inputProvider.DidNotReceive().Select(Questions.SelectUpdateStrategy, Arg.Any<UpdateStrategy[]>());
        gitClient.Received().ChangeBranch(branch1);
    }

    [Fact]
    public async Task WhenMergeIsProvided_SyncsStackUsingMerge()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, false, true, false, false));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<StackStatus>());
        stackActions.Received().PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, false);
        gitClient.Received().ChangeBranch(branch1);
    }

    [Fact]
    public async Task WhenNotSpecifyingRebaseOrMerge_AndUpdateSettingIsRebase_SyncsStackUsingRebase()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        stackActions.GetUpdateStrategyConfigValue().Returns(UpdateStrategy.Rebase);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, null, null, false, false));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Rebase, Arg.Any<StackStatus>());
        stackActions.Received().PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, true);
        gitClient.Received().ChangeBranch(branch1);
    }

    [Fact]
    public async Task WhenNotSpecifyingRebaseOrMerge_AndUpdateSettingIsMerge_SyncsStackUsingMerge()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        stackActions.GetUpdateStrategyConfigValue().Returns(UpdateStrategy.Merge);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, null, null, false, false));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<StackStatus>());
        stackActions.Received().PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, false);
        gitClient.Received().ChangeBranch(branch1);
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToMerge_ButRebaseIsSpecified_SyncsStackUsingRebase()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        stackActions.GetUpdateStrategyConfigValue().Returns(UpdateStrategy.Merge);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, true, null, false, false));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Rebase, Arg.Any<StackStatus>());
        stackActions.Received().PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, true);
        gitClient.Received().ChangeBranch(branch1);
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToRebase_ButMergeIsSpecified_SyncsStackUsingMerge()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        stackActions.GetUpdateStrategyConfigValue().Returns(UpdateStrategy.Rebase);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, null, true, false, false));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<StackStatus>());
        stackActions.Received().PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, false);
        gitClient.Received().ChangeBranch(branch1);
    }

    [Fact]
    public async Task WhenNotSpecifyingRebaseOrMerge_AndNoUpdateSettingExists_AndMergeIsSelected_SyncsStackUsingMerge()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        stackActions.GetUpdateStrategyConfigValue().Returns((UpdateStrategy?)null);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectUpdateStrategy, Arg.Any<UpdateStrategy[]>()).Returns(UpdateStrategy.Merge);
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, null, null, false, false));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<StackStatus>());
        stackActions.Received().PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, false);
        gitClient.Received().ChangeBranch(branch1);
    }

    [Fact]
    public async Task WhenNotSpecifyingRebaseOrMerge_AndNoUpdateSettingsExists_AndRebaseIsSelected_SyncsStackUsingRebase()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1).WithChildBranch(b => b.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        stackActions.GetUpdateStrategyConfigValue().Returns((UpdateStrategy?)null);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Select(Questions.SelectUpdateStrategy, Arg.Any<UpdateStrategy[]>()).Returns(UpdateStrategy.Rebase);
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, null, null, false, false));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Rebase, Arg.Any<StackStatus>());
        stackActions.Received().PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, true);
        gitClient.Received().ChangeBranch(branch1);
    }

    [Fact]
    public async Task WhenBothRebaseAndMergeAreSpecified_AnErrorIsThrown()
    {
        // Arrange
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        // Act and assert
        await handler
            .Invoking(h => h.Handle(new SyncStackCommandInputs(null, 5, true, true, false, false)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot specify both rebase and merge.");
    }

    [Fact]
    public async Task WhenConfirmOptionIsProvided_DoesNotAskForConfirmation()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, false, false, true, false));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<StackStatus>());
        stackActions.Received().PushChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), 5, false);
        gitClient.Received().ChangeBranch(branch1);
        inputProvider.DidNotReceive().Confirm(Questions.ConfirmSyncStack);
    }

    [Fact]
    public async Task WhenNoPushOptionIsProvided_DoesNotPushChangesToRemote()
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
                .WithBranch(stackBranch => stackBranch.WithName(branch1))
                .WithBranch(stackBranch => stackBranch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        var handler = new SyncStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig, stackActions);

        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(branch1);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            { sourceBranch, new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch1, new GitBranchStatus(branch1, $"origin/{branch1}", true, true, 0, 0, new Commit("abc1234", "Test commit message")) },
            { branch2, new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit("def5678", "Test commit message")) }
        });

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmSyncStack).Returns(true);

        // Act
        await handler.Handle(new SyncStackCommandInputs(null, 5, false, false, false, true));

        // Assert
        stackActions.Received().PullChanges(Arg.Is<Config.Stack>(s => s.Name == "Stack1"));
        stackActions.Received().UpdateStack(Arg.Is<Config.Stack>(s => s.Name == "Stack1"), UpdateStrategy.Merge, Arg.Any<StackStatus>());
        stackActions.DidNotReceive().PushChanges(Arg.Any<Config.Stack>(), Arg.Any<int>(), Arg.Any<bool>());
        gitClient.Received().ChangeBranch(branch1);
    }
}
