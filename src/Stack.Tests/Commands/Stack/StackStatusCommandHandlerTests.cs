using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Config;
using Stack.Git;
using Stack.Tests.Helpers;
using Stack.Infrastructure;
using Stack.Commands.Helpers;

namespace Stack.Tests.Commands.Stack;

public class StackStatusCommandHandlerTests
{
    [Fact]
    public async Task WhenMultipleBranchesExistInAStack_AndOneHasAPullRequests_ReturnsStatus()
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
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);

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
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new StackStatusCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        logger.WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>())).Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubClient.GetPullRequest(branch1).Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false, true));

        // Assert
        var expectedSourceBranch = new BranchDetailBase(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));
        var expectedBranch1 = new BranchDetail(branch1, true,
            new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
            pr, new ParentBranchStatus(expectedSourceBranch, 10, 5));
        var expectedBranch2 = new BranchDetail(branch2, true,
            new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
            null, new ParentBranchStatus(expectedBranch1, 1, 0));

        var expectedStackDetail = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1, expectedBranch2]);
        response.Stacks.Should().BeEquivalentTo([expectedStackDetail]);
    }

    [Fact]
    public async Task WhenStackNameIsProvided_DoesNotAskForStack_ReturnsStatus()
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
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);

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
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new StackStatusCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        logger.WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>())).Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubClient.GetPullRequest(branch1).Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs("Stack1", false, true));

        // Assert
        var expectedSourceBranch = new BranchDetailBase(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));

        var expectedBranch1 = new BranchDetail(branch1, true,
            new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
            pr, new ParentBranchStatus(expectedSourceBranch, 10, 5));

        var expectedBranch2 = new BranchDetail(branch2, true,
            new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
            null, new ParentBranchStatus(expectedBranch1, 1, 0));

        var expectedStackDetail = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1, expectedBranch2]);
        response.Stacks.Should().BeEquivalentTo([expectedStackDetail]);

        inputProvider.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task WhenAllStacksAreRequested_ReturnsStatusOfEachStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var branch3 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithBranch(builder => builder.WithName(branch3).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(3).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);
        var tipOfBranch3 = repo.GetTipOfBranch(branch3);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branch3)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new StackStatusCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        logger
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubClient
            .GetPullRequest(branch1)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, true, true));

        // Assert
        var expectedSourceBranch = new BranchDetailBase(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));

        var expectedBranch1 = new BranchDetail(branch1, true,
            new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
            pr, new ParentBranchStatus(expectedSourceBranch, 10, 5));

        var expectedBranch2 = new BranchDetail(branch2, true,
            new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
            null, new ParentBranchStatus(expectedBranch1, 1, 0));

        var expectedStackDetail1 = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1, expectedBranch2]);

        var expectedBranch3 = new BranchDetail(branch3, true,
            new Commit(tipOfBranch3.Sha[..7], tipOfBranch3.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch3}", true, 0, 0),
            null, new ParentBranchStatus(expectedSourceBranch, 3, 5));

        var expectedStackDetail2 = new StackStatus("Stack2", expectedSourceBranch, [expectedBranch3]);

        response.Stacks.Should().BeEquivalentTo([expectedStackDetail1, expectedStackDetail2]);
    }

    [Fact]
    public async Task WhenAllStacksAreRequested_WithStacksInMultipleRepositories_ReturnsStatusOfEachStackInTheCorrectRepository()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var branch3 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithBranch(builder => builder.WithName(branch3).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(3).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);
        var tipOfBranch3 = repo.GetTipOfBranch(branch3);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branch3)))
            .WithStack(stack => stack
                .WithName("Stack3")
                .WithRemoteUri(Some.HttpsUri().ToString())
                .WithSourceBranch(Some.BranchName())
                .WithBranch(b => b.WithName(Some.BranchName())))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new StackStatusCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        logger.WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>())).Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubClient.GetPullRequest(branch1).Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, true, true));

        // Assert
        var expectedSourceBranch = new BranchDetailBase(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));

        var expectedBranch1 = new BranchDetail(branch1, true,
            new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
            pr, new ParentBranchStatus(expectedSourceBranch, 10, 5));

        var expectedBranch2 = new BranchDetail(branch2, true,
            new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
            null, new ParentBranchStatus(expectedBranch1, 1, 0));

        var expectedStackDetail1 = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1, expectedBranch2]);

        var expectedBranch3 = new BranchDetail(branch3, true,
            new Commit(tipOfBranch3.Sha[..7], tipOfBranch3.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch3}", true, 0, 0),
            null, new ParentBranchStatus(expectedSourceBranch, 3, 5));

        var expectedStackDetail2 = new StackStatus("Stack2", expectedSourceBranch, [expectedBranch3]);

        response.Stacks.Should().BeEquivalentTo([expectedStackDetail1, expectedStackDetail2]);
    }

    [Fact]
    public async Task WhenStackNameIsProvided_ButStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var aBranch = Some.BranchName();
        var aSecondBranch = Some.BranchName();
        var aThirdBranch = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).PushToRemote())
            .WithBranch(builder => builder.WithName(aBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10).PushToRemote())
            .WithBranch(builder => builder.WithName(aSecondBranch).FromSourceBranch(aBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .WithBranch(builder => builder.WithName(aThirdBranch).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(3).PushToRemote())
            .WithNumberOfEmptyCommits(b => b.OnBranch(sourceBranch).PushToRemote(), 5)
            .Build();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(aBranch).WithChildBranch(b2 => b2.WithName(aSecondBranch))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(aThirdBranch)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new StackStatusCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        // Act and assert
        var incorrectStackName = Some.Name();
        await handler
            .Invoking(async h => await h.Handle(new StackStatusCommandInputs(incorrectStackName, false, false)))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{incorrectStackName}' not found.");
    }

    [Fact]
    public async Task WhenMultipleBranchesExistInAStack_AndOneNoLongerExistsOnTheRemote_ReturnsCorrectStatus()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = "branch-1";
        var branch2 = "branch-2";
        var branch3 = "branch-3";
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).WithNumberOfEmptyCommits(5).PushToRemote())
            .WithBranch(builder => builder.WithName(branch1).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(10))
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(branch1).WithNumberOfEmptyCommits(1).PushToRemote())
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branch3)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new StackStatusCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        logger
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubClient
            .GetPullRequest(branch2)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false, true));

        // Assert
        var expectedSourceBranch = new BranchDetailBase(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));

        var expectedBranch1 = new BranchDetail(branch1, true,
            new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim()),
            null, null, new ParentBranchStatus(expectedSourceBranch, 0, 0));

        var expectedBranch2 = new BranchDetail(branch2, true,
            new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
            pr, new ParentBranchStatus(expectedSourceBranch, 11, 0));

        var expectedStackDetail = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1, expectedBranch2]);
        response.Stacks.Should().BeEquivalentTo([expectedStackDetail]);
    }

    [Fact]
    public async Task WhenMultipleBranchesExistInAStack_AndOneNoLongerExistsOnTheRemoteAndLocally_ReturnsCorrectStatus()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var branch3 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(builder => builder.WithName(sourceBranch).WithNumberOfEmptyCommits(5).PushToRemote())
            .WithBranch(builder => builder.WithName(branch2).FromSourceBranch(sourceBranch).WithNumberOfEmptyCommits(1).PushToRemote())
            .Build();

        var tipOfSourceBranch = repo.GetTipOfBranch(sourceBranch);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branch3)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new StackStatusCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        logger
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubClient
            .GetPullRequest(branch2)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false, true));

        // Assert
        var expectedSourceBranch = new BranchDetailBase(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));

        var expectedBranch1 = new BranchDetail(branch1, false, null, null, null, null);

        var expectedBranch2 = new BranchDetail(branch2, true,
            new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
            pr, new ParentBranchStatus(expectedSourceBranch, 1, 0));

        var expectedStackDetail = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1, expectedBranch2]);
        response.Stacks.Should().BeEquivalentTo([expectedStackDetail]);
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_ReturnsStatus()
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
        var tipOfBranch1 = repo.GetTipOfBranch(branch1);
        var tipOfBranch2 = repo.GetTipOfBranch(branch2);

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = Substitute.For<ILogger>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var gitHubClient = Substitute.For<IGitHubClient>();
        var handler = new StackStatusCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        logger
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false);

        gitHubClient
            .GetPullRequest(branch1)
            .Returns(pr);

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false, true));

        // Assert
        var expectedSourceBranch = new BranchDetailBase(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));
        var expectedBranch1 = new BranchDetail(branch1, true,
            new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
            pr, new ParentBranchStatus(expectedSourceBranch, 10, 5));
        var expectedBranch2 = new BranchDetail(branch2, true,
            new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
            null, new ParentBranchStatus(expectedBranch1, 1, 0));

        var expectedStackDetail = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1, expectedBranch2]);
        response.Stacks.Should().BeEquivalentTo([expectedStackDetail]);

        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }
}
