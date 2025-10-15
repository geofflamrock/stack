using FluentAssertions;
using Meziantou.Extensions.Logging.Xunit;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Stack;

public class StackStatusCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenMultipleBranchesExistInAStack_AndOneHasAPullRequests_ReturnsStatus()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var tipOfSourceBranch = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch1 = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch2 = new Commit(Some.Sha(), Some.Name());

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackStatusCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new StackStatusCommandHandler(inputProvider, logger, console, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1);

        gitHubClient.GetPullRequest(branch1).Returns(pr);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, tipOfSourceBranch),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, tipOfBranch1),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, tipOfBranch2),
        });
        gitClient.CompareBranches(branch1, sourceBranch).Returns((10, 5));
        gitClient.CompareBranches(branch2, branch1).Returns((1, 0));

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false, true), CancellationToken.None);

        // Assert
        var expectedSourceBranch = new SourceBranchDetail(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));
        var expectedBranch1 = new BranchDetail(branch1, true,
            new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
            pr, new ParentBranchStatus(sourceBranch, 10, 5),
            [
                new BranchDetail(branch2, true,
                    new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
                    new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
                    null, new ParentBranchStatus(branch1, 1, 0), [])
            ]);

        var expectedStackDetail = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1]);
        response.Stacks.Should().BeEquivalentTo([expectedStackDetail]);
    }

    [Fact]
    public async Task WhenStackNameIsProvided_DoesNotAskForStack_ReturnsStatus()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var tipOfSourceBranch = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch1 = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch2 = new Commit(Some.Sha(), Some.Name());

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackStatusCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new StackStatusCommandHandler(inputProvider, logger, console, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1);

        gitHubClient.GetPullRequest(branch1).Returns(pr);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, tipOfSourceBranch),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, tipOfBranch1),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, tipOfBranch2),
        });
        gitClient.CompareBranches(branch1, sourceBranch).Returns((10, 5));
        gitClient.CompareBranches(branch2, branch1).Returns((1, 0));

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs("Stack1", false, true), CancellationToken.None);

        // Assert
        var expectedSourceBranch = new SourceBranchDetail(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));

        var expectedBranch1 = new BranchDetail(branch1, true,
            new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
            pr, new ParentBranchStatus(sourceBranch, 10, 5),
            [
                new BranchDetail(branch2, true,
                    new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
                    new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
                    null, new ParentBranchStatus(branch1, 1, 0), [])
            ]);

        var expectedStackDetail = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1]);
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
        var tipOfSourceBranch = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch1 = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch2 = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch3 = new Commit(Some.Sha(), Some.Name());

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branch3)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackStatusCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new StackStatusCommandHandler(inputProvider, logger, console, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1);

        gitHubClient
            .GetPullRequest(branch1)
            .Returns(pr);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, tipOfSourceBranch),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, tipOfBranch1),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, tipOfBranch2),
            [branch3] = new GitBranchStatus(branch3, $"origin/{branch3}", true, false, 0, 0, tipOfBranch3),
        });
        gitClient.CompareBranches(branch1, sourceBranch).Returns((10, 5));
        gitClient.CompareBranches(branch2, branch1).Returns((1, 0));
        gitClient.CompareBranches(branch3, sourceBranch).Returns((3, 5));

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, true, true), CancellationToken.None);

        // Assert
        var expectedSourceBranch = new SourceBranchDetail(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));

        var expectedBranch1 = new BranchDetail(branch1, true,
            new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
            pr, new ParentBranchStatus(sourceBranch, 10, 5),
            [
                new BranchDetail(branch2, true,
                    new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
                    new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
                    null, new ParentBranchStatus(branch1, 1, 0), [])
            ]);

        var expectedStackDetail1 = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1]);

        var expectedBranch3 = new BranchDetail(branch3, true,
            new Commit(tipOfBranch3.Sha[..7], tipOfBranch3.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch3}", true, 0, 0),
            null, new ParentBranchStatus(sourceBranch, 3, 5), []);

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
        var otherRemoteUri = Some.HttpsUri().ToString();
        var tipOfSourceBranch = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch1 = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch2 = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch3 = new Commit(Some.Sha(), Some.Name());

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branch3)))
            .WithStack(stack => stack
                .WithName("Stack3")
                .WithRemoteUri(otherRemoteUri)
                .WithSourceBranch(Some.BranchName())
                .WithBranch(b => b.WithName(Some.BranchName())))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackStatusCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new StackStatusCommandHandler(inputProvider, logger, console, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1);

        gitHubClient.GetPullRequest(branch1).Returns(pr);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, tipOfSourceBranch),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, tipOfBranch1),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, tipOfBranch2),
            [branch3] = new GitBranchStatus(branch3, $"origin/{branch3}", true, false, 0, 0, tipOfBranch3),
        });
        gitClient.CompareBranches(branch1, sourceBranch).Returns((10, 5));
        gitClient.CompareBranches(branch2, branch1).Returns((1, 0));
        gitClient.CompareBranches(branch3, sourceBranch).Returns((3, 5));

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, true, true), CancellationToken.None);

        // Assert
        var expectedSourceBranch = new SourceBranchDetail(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));

        var expectedBranch1 = new BranchDetail(branch1, true,
            new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
            pr, new ParentBranchStatus(sourceBranch, 10, 5),
            [
                new BranchDetail(branch2, true,
                    new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
                    new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
                    null, new ParentBranchStatus(branch1, 1, 0), [])
            ]);

        var expectedStackDetail1 = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1]);

        var expectedBranch3 = new BranchDetail(branch3, true,
            new Commit(tipOfBranch3.Sha[..7], tipOfBranch3.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch3}", true, 0, 0),
            null, new ParentBranchStatus(sourceBranch, 3, 5), []);

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

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(aBranch).WithChildBranch(b2 => b2.WithName(aSecondBranch))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(aThirdBranch)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackStatusCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new StackStatusCommandHandler(inputProvider, logger, console, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetCurrentBranch().Returns(sourceBranch);

        // Act and assert
        var incorrectStackName = Some.Name();
        await handler
            .Invoking(async h => await h.Handle(new StackStatusCommandInputs(incorrectStackName, false, false), CancellationToken.None))
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
        var tipOfSourceBranch = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch1 = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch2 = new Commit(Some.Sha(), Some.Name());

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branch3)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackStatusCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new StackStatusCommandHandler(inputProvider, logger, console, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch2);

        gitHubClient
            .GetPullRequest(branch2)
            .Returns(pr);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, tipOfSourceBranch),
            // branch1 exists locally but has no remote tracking branch
            [branch1] = new GitBranchStatus(branch1, null, false, false, 0, 0, tipOfBranch1),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, tipOfBranch2),
        });
        // Since branch1 is inactive (no remote), parent of branch2 becomes sourceBranch
        gitClient.CompareBranches(branch2, sourceBranch).Returns((11, 0));

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false, true), CancellationToken.None);

        // Assert
        var expectedSourceBranch = new SourceBranchDetail(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));

        var expectedBranch1 = new BranchDetail(branch1, true,
            new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim()),
            null, null, new ParentBranchStatus(sourceBranch, 0, 0),
            [
                new BranchDetail(branch2, true,
                    new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
                    new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
                    pr, new ParentBranchStatus(sourceBranch, 11, 0), [])
            ]);

        var expectedStackDetail = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1]);
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
        var tipOfSourceBranch = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch2 = new Commit(Some.Sha(), Some.Name());

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(branch3)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackStatusCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new StackStatusCommandHandler(inputProvider, logger, console, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch2);

        gitHubClient
            .GetPullRequest(branch2)
            .Returns(pr);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, tipOfSourceBranch),
            // branch1 missing locally and remotely => not present in statuses dictionary
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, tipOfBranch2),
        });
        gitClient.CompareBranches(branch2, sourceBranch).Returns((1, 0));

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false, true), CancellationToken.None);

        // Assert
        var expectedSourceBranch = new SourceBranchDetail(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));

        var expectedBranch1 = new BranchDetail(branch1, false, null, null, null, null,
        [
            new BranchDetail(branch2, true,
                new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
                new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
                pr, new ParentBranchStatus(sourceBranch, 1, 0), [])
        ]);

        var expectedStackDetail = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1]);
        response.Stacks.Should().BeEquivalentTo([expectedStackDetail]);
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_ReturnsStatus()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var tipOfSourceBranch = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch1 = new Commit(Some.Sha(), Some.Name());
        var tipOfBranch2 = new Commit(Some.Sha(), Some.Name());

        var stackRepository = new TestStackRepositoryBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithSourceBranch(sourceBranch)
                .WithBranch(b1 => b1.WithName(branch1).WithChildBranch(b2 => b2.WithName(branch2))))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<StackStatusCommandHandler>(testOutputHelper);
        var gitClient = Substitute.For<IGitClient>();
        var gitHubClient = Substitute.For<IGitHubClient>();
        var console = new TestDisplayProvider(testOutputHelper);
        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };
        var handler = new StackStatusCommandHandler(inputProvider, logger, console, gitClientFactory, executionContext, gitHubClient, stackRepository);

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);

        var pr = new GitHubPullRequest(1, "PR title", "PR body", GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1);

        gitHubClient
            .GetPullRequest(branch1)
            .Returns(pr);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, tipOfSourceBranch),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, tipOfBranch1),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, tipOfBranch2),
        });
        gitClient.CompareBranches(branch1, sourceBranch).Returns((10, 5));
        gitClient.CompareBranches(branch2, branch1).Returns((1, 0));

        // Act
        var response = await handler.Handle(new StackStatusCommandInputs(null, false, true), CancellationToken.None);

        // Assert
        var expectedSourceBranch = new SourceBranchDetail(sourceBranch, true,
            new Commit(tipOfSourceBranch.Sha[..7], tipOfSourceBranch.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{sourceBranch}", true, 0, 0));
        var expectedBranch1 = new BranchDetail(branch1, true,
            new Commit(tipOfBranch1.Sha[..7], tipOfBranch1.Message.Trim()),
            new RemoteTrackingBranchStatus($"origin/{branch1}", true, 0, 0),
            pr, new ParentBranchStatus(sourceBranch, 10, 5),
            [
                new BranchDetail(branch2, true,
                    new Commit(tipOfBranch2.Sha[..7], tipOfBranch2.Message.Trim()),
                    new RemoteTrackingBranchStatus($"origin/{branch2}", true, 0, 0),
                    null, new ParentBranchStatus(branch1, 1, 0), [])
            ]);

        var expectedStackDetail = new StackStatus("Stack1", expectedSourceBranch, [expectedBranch1]);
        response.Stacks.Should().BeEquivalentTo([expectedStackDetail]);

        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }
}
