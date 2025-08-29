using FluentAssertions;
using FluentAssertions.Equivalency;
using NSubstitute;
using Meziantou.Extensions.Logging.Xunit;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;
using Xunit.Abstractions;
using static Stack.Commands.CreatePullRequestsCommandHandler;

namespace Stack.Tests.Commands.PullRequests;

public class CreatePullRequestsCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenNoPullRequestsExistForAStackWithMultipleBranches_CreatesPullRequestForEachBranchTargetingTheCorrectParentBranch()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<CreatePullRequestsCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var fileOperations = new FileOperations();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetRootOfRepository().Returns(TemporaryDirectory.CreatePath());
        gitClient.CompareBranches(Arg.Any<string>(), Arg.Any<string>()).Returns((0, 0));
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), "msg"))
        });
        var handler = new CreatePullRequestsCommandHandler(inputProvider, logger, console, gitClient, gitHubClient, fileOperations, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), true, Arg.Any<CancellationToken>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch), new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests, Arg.Any<CancellationToken>()).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle, Arg.Any<CancellationToken>()).Returns("PR Title");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty, CancellationToken.None);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1),
            [branch2] = new GitHubPullRequest(2, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch2)
        };

        gitHubClient.PullRequests.Should().BeEquivalentTo(expectedPullRequests, ExcludeUnimportantPullRequestProperties);
    }

    private static EquivalencyAssertionOptions<Dictionary<string, GitHubPullRequest>> ExcludeUnimportantPullRequestProperties(EquivalencyAssertionOptions<Dictionary<string, GitHubPullRequest>> opt)
    {
        return opt.Excluding(member =>
            member.DeclaringType == typeof(GitHubPullRequest) &&
            (member.Name.Equals(nameof(GitHubPullRequest.Url)) || member.Name.Equals(nameof(GitHubPullRequest.Number))));
    }

    [Fact]
    public async Task WhenCreatingPullRequestsForAStackWithMultipleBranches_EachPullRequestHasTheCorrectPullRequestList()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<CreatePullRequestsCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var fileOperations = new FileOperations();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetRootOfRepository().Returns(TemporaryDirectory.CreatePath());
        gitClient.CompareBranches(Arg.Any<string>(), Arg.Any<string>()).Returns((0, 0));
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), "msg"))
        });
        var handler = new CreatePullRequestsCommandHandler(inputProvider, logger, console, gitClient, gitHubClient, fileOperations, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), true, Arg.Any<CancellationToken>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch), new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests, Arg.Any<CancellationToken>()).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle, Arg.Any<CancellationToken>()).Returns("PR Title");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty, CancellationToken.None);

        // Assert
        var expectedPullRequestList = $@"{StackConstants.StackMarkerStart}
{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        gitHubClient.PullRequests.Should().AllSatisfy(pr => pr.Value.Body.Should().Be(expectedPullRequestList));
    }

    [Fact]
    public async Task WhenAPullRequestExistForABranch_AndNoneForAnotherBranch_CreatesPullRequestForTheCorrectBranchAndSetsPullRequestList()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var gitHubClient = new TestGitHubRepositoryBuilder()
            .WithPullRequest(branch1, pr => pr.WithBody($"{StackConstants.StackMarkerStart} {StackConstants.StackMarkerEnd}"))
            .Build();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<CreatePullRequestsCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var fileOperations = new FileOperations();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetRootOfRepository().Returns(TemporaryDirectory.CreatePath());
        gitClient.CompareBranches(Arg.Any<string>(), Arg.Any<string>()).Returns((0, 0));
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), "msg"))
        });
        var handler = new CreatePullRequestsCommandHandler(inputProvider, logger, console, gitClient, gitHubClient, fileOperations, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), true, Arg.Any<CancellationToken>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests, Arg.Any<CancellationToken>()).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle, Arg.Any<CancellationToken>()).Returns("PR Title");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty, CancellationToken.None);

        // Assert
        var pullRequestUrls = gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}").ToArray();
        var expectedPullRequestList = $@"{StackConstants.StackMarkerStart}
{string.Join(Environment.NewLine, pullRequestUrls)}
{StackConstants.StackMarkerEnd}";

        gitHubClient.PullRequests.Should().AllSatisfy(pr => pr.Value.Body.Should().Be(expectedPullRequestList));
    }

    [Fact]
    public async Task WhenStackNameIsProvided_PullRequestsAreCreatedForThatStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<CreatePullRequestsCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var fileOperations = new FileOperations();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetRootOfRepository().Returns(TemporaryDirectory.CreatePath());
        gitClient.CompareBranches(Arg.Any<string>(), Arg.Any<string>()).Returns((0, 0));
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), "msg"))
        });
        var handler = new CreatePullRequestsCommandHandler(inputProvider, logger, console, gitClient, gitHubClient, fileOperations, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), true, Arg.Any<CancellationToken>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch), new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests, Arg.Any<CancellationToken>()).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle, Arg.Any<CancellationToken>()).Returns("PR Title");

        // Act
        await handler.Handle(new CreatePullRequestsCommandInputs("Stack1"), CancellationToken.None);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1),
            [branch2] = new GitHubPullRequest(2, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch2)
        };

        gitHubClient.PullRequests.Should().BeEquivalentTo(expectedPullRequests, ExcludeUnimportantPullRequestProperties);
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_PullRequestsAreCreatedForThatStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<CreatePullRequestsCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var fileOperations = new FileOperations();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetRootOfRepository().Returns(TemporaryDirectory.CreatePath());
        gitClient.CompareBranches(Arg.Any<string>(), Arg.Any<string>()).Returns((0, 0));
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), "msg"))
        });
        var handler = new CreatePullRequestsCommandHandler(inputProvider, logger, console, gitClient, gitHubClient, fileOperations, stackConfig);


        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), true, Arg.Any<CancellationToken>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch), new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests, Arg.Any<CancellationToken>()).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle, Arg.Any<CancellationToken>()).Returns("PR Title");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty, CancellationToken.None);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1),
            [branch2] = new GitHubPullRequest(2, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch2)
        };

        gitHubClient.PullRequests.Should().BeEquivalentTo(expectedPullRequests, ExcludeUnimportantPullRequestProperties);
        await inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenStackNameIsProvided_ButTheStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<CreatePullRequestsCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var fileOperations = new FileOperations();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetRootOfRepository().Returns(TemporaryDirectory.CreatePath());
        gitClient.CompareBranches(Arg.Any<string>(), Arg.Any<string>()).Returns((0, 0));
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), "msg"))
        });
        var handler = new CreatePullRequestsCommandHandler(inputProvider, logger, console, gitClient, gitHubClient, fileOperations, stackConfig);


        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(h => h.Handle(new CreatePullRequestsCommandInputs(invalidStackName), CancellationToken.None))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenAPullRequestExistForABranch_AndHasBeenMerged_AndNoneForAnotherBranch_CreatesPullRequestTargetingTheCorrectBranchAndSetsDescription()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var gitHubClient = new TestGitHubRepositoryBuilder()
            .WithPullRequest(branch1, pr => pr
                .WithBody($"{StackConstants.StackMarkerStart} {StackConstants.StackMarkerEnd}")
                .WithTitle("PR Title")
                .Merged())
            .Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<CreatePullRequestsCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var fileOperations = new FileOperations();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetRootOfRepository().Returns(TemporaryDirectory.CreatePath());
        gitClient.CompareBranches(Arg.Any<string>(), Arg.Any<string>()).Returns((0, 0));
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), "msg"))
        });
        var handler = new CreatePullRequestsCommandHandler(inputProvider, logger, console, gitClient, gitHubClient, fileOperations, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), true, Arg.Any<CancellationToken>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch2, sourceBranch)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests, Arg.Any<CancellationToken>()).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle, Arg.Any<CancellationToken>()).Returns("PR Title");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty, CancellationToken.None);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Merged, Some.HttpsUri(), false, branch1),
            [branch2] = new GitHubPullRequest(2, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch2)
        };

        gitHubClient.PullRequests.Should().BeEquivalentTo(expectedPullRequests, ExcludeUnimportantPullRequestProperties);
    }

    [Fact]
    public async Task WhenAPullRequestTemplateExistsInTheRepo_ItIsUsedAsTheBodyOfANewPullRequest()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<CreatePullRequestsCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var fileOperations = new FileOperations();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        using var tempRepo = TemporaryDirectory.Create();
        gitClient.GetRootOfRepository().Returns(tempRepo.DirectoryPath);
        gitClient.CompareBranches(Arg.Any<string>(), Arg.Any<string>()).Returns((0, 0));
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), "msg"))
        });
        var handler = new CreatePullRequestsCommandHandler(inputProvider, logger, console, gitClient, gitHubClient, fileOperations, stackConfig);
        Directory.CreateDirectory(Path.Join(tempRepo.DirectoryPath, ".github"));
        File.WriteAllText(Path.Join(tempRepo.DirectoryPath, ".github", "PULL_REQUEST_TEMPLATE.md"), "This is the PR template");


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), true, Arg.Any<CancellationToken>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch), new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests, Arg.Any<CancellationToken>()).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle, Arg.Any<CancellationToken>()).Returns("PR Title");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty, CancellationToken.None);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}
This is the PR template";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1),
            [branch2] = new GitHubPullRequest(2, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch2)
        };

        gitHubClient.PullRequests.Should().BeEquivalentTo(expectedPullRequests, ExcludeUnimportantPullRequestProperties);
    }

    [Fact]
    public async Task WhenAPullRequestTemplateDoesNotExistInTheRepo_TheStackPrListMarkersArePutIntoTheBody()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<CreatePullRequestsCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var fileOperations = new FileOperations();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.CompareBranches(Arg.Any<string>(), Arg.Any<string>()).Returns((0, 0));
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), "msg"))
        });
        var handler = new CreatePullRequestsCommandHandler(inputProvider, logger, console, gitClient, gitHubClient, fileOperations, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), true, Arg.Any<CancellationToken>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests, Arg.Any<CancellationToken>()).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle, Arg.Any<CancellationToken>()).Returns("PR Title");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty, CancellationToken.None);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
            
{StackConstants.StackMarkerDescription}

{StackConstants.StackMarkerEnd}";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1)
        };

        gitHubClient.PullRequests.Should().BeEquivalentTo(expectedPullRequests, ExcludeUnimportantPullRequestProperties);
    }

    [Fact]
    public async Task WhenAskedWhetherToCreateAPullRequestAsADraft_AndTheAnswerIsYes_PullRequestsCreatedAsADraft()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<CreatePullRequestsCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var fileOperations = new FileOperations();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetRootOfRepository().Returns(TemporaryDirectory.CreatePath());
        gitClient.CompareBranches(Arg.Any<string>(), Arg.Any<string>()).Returns((0, 0));
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), "msg"))
        });
        var handler = new CreatePullRequestsCommandHandler(inputProvider, logger, console, gitClient, gitHubClient, fileOperations, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), true, Arg.Any<CancellationToken>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch), new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests, Arg.Any<CancellationToken>()).Returns(true);
        inputProvider.Confirm(Questions.CreatePullRequestAsDraft, Arg.Any<CancellationToken>(), Arg.Any<bool>()).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle, Arg.Any<CancellationToken>()).Returns("PR Title");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty, CancellationToken.None);

        // Assert
        gitHubClient.PullRequests.Should().AllSatisfy(pr => pr.Value.IsDraft.Should().BeTrue());
    }

    [Fact]
    public async Task WhenOnlySelectingSomeBranchesToCreatePullRequestsFor_OnlyThosePullRequestsAreCreated()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<CreatePullRequestsCommandHandler>(testOutputHelper);
        var console = new TestDisplayProvider(testOutputHelper);
        var fileOperations = new FileOperations();
        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(sourceBranch);
        gitClient.GetRootOfRepository().Returns(TemporaryDirectory.CreatePath());
        gitClient.CompareBranches(Arg.Any<string>(), Arg.Any<string>()).Returns((0, 0));
        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(new Dictionary<string, GitBranchStatus>
        {
            [sourceBranch] = new GitBranchStatus(sourceBranch, $"origin/{sourceBranch}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch1] = new GitBranchStatus(branch1, $"origin/{branch1}", true, false, 0, 0, new Commit(Some.Sha(), "msg")),
            [branch2] = new GitBranchStatus(branch2, $"origin/{branch2}", true, false, 0, 0, new Commit(Some.Sha(), "msg"))
        });
        var handler = new CreatePullRequestsCommandHandler(inputProvider, logger, console, gitClient, gitHubClient, fileOperations, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), true, Arg.Any<CancellationToken>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests, Arg.Any<CancellationToken>()).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle, Arg.Any<CancellationToken>()).Returns("PR Title");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty, CancellationToken.None);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
            
{StackConstants.StackMarkerDescription}

{StackConstants.StackMarkerEnd}";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1)
        };

        gitHubClient.PullRequests.Should().BeEquivalentTo(expectedPullRequests, ExcludeUnimportantPullRequestProperties);
    }
}
