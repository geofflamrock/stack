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
    public async Task WhenMultipleBranchesExistInAStack_UpdatesAndMergesEachBranchInSequence()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false, true));

        // Assert
        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);
    }

    [Fact]
    public async Task WhenABranchInTheStackNoLongerExistsOnTheRemote_SkipsOverUpdatingThatBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10))
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false, true));

        // Assert
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
    }

    [Fact]
    public async Task WhenABranchInTheStackExistsOnTheRemote_ButThePullRequestIsMerged_SkipsOverUpdatingThatBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        gitHubClient.GetPullRequest(branch1).Returns(new GitHubPullRequest(1, Some.Name(), Some.Name(), GitHubPullRequestStates.Merged, Some.HttpsUri(), false));

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false, true));

        // Assert
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
    }

    [Fact]
    public async Task WhenNameIsProvided_DoesNotAskForName_UpdatesCorrectStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        // Act
        await handler.Handle(new UpdateStackCommandInputs("Stack1", false, true));

        // Assert
        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenNameIsProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


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
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        // We are on a specific branch in the stack
        gitClient.ChangeBranch(branch1);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false, true));

        // Assert
        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);
        gitClient.GetCurrentBranch().Should().Be(branch1);
    }

    [Fact]
    public async Task WhenOnlyASingleStackExists_DoesNotAskForStackName_UpdatesStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, false, true));

        // Assert
        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenUpdatingUsingRebase_AllBranchesInStackAreUpdated()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch2).PushToRemote(), 1)
            .Build();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, true, false));

        // Assert
        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetAheadBehind(branch2).Should().Be((20, 12));
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToRebase_AllBranchesInStackAreUpdatedUsingRebase()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch2).PushToRemote(), 1)
            .WithConfig("stack.update.strategy", "rebase")
            .Build();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, null));

        // Assert
        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetAheadBehind(branch2).Should().Be((20, 12));
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToRebase_ButMergeIsSpecified_AllBranchesInStackAreUpdatedUsingMerge()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch2).PushToRemote(), 1)
            .WithConfig("stack.update.strategy", "rebase")
            .Build();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, true));

        // Assert
        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetAheadBehind(branch2).Should().Be((10, 0));
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToMerge_AllBranchesInStackAreUpdatedUsingMerge()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch2).PushToRemote(), 1)
            .WithConfig("stack.update.strategy", "merge")
            .Build();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, null, null));

        // Assert
        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetAheadBehind(branch2).Should().Be((10, 0));
    }

    [Fact]
    public async Task WhenGitConfigValueIsSetToMerge_ButRebaseIsSpecified_AllBranchesInStackAreUpdatedUsingRebase()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch1).PushToRemote(), 3)
            .WithNumberOfEmptyCommits(b => b.OnBranch(branch2).PushToRemote(), 1)
            .WithConfig("stack.update.strategy", "merge")
            .Build();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        // Act
        await handler.Handle(new UpdateStackCommandInputs(null, true, null));

        // Assert
        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);

        repo.GetCommitsReachableFromBranch(branch1).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfSourceBranch);
        repo.GetCommitsReachableFromBranch(branch2).Should().Contain(tipOfBranch1);
        repo.GetAheadBehind(branch2).Should().Be((20, 12));
    }

    [Fact]
    public async Task WhenBothRebaseAndMergeAreSpecified_AnErrorIsThrown()
    {
        // Arrange
        var stackConfig = new TestStackConfigBuilder().Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new UpdateStackCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        // Act and assert
        await handler
            .Invoking(h => h.Handle(new UpdateStackCommandInputs(null, true, true)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot specify both rebase and merge.");
    }
}
