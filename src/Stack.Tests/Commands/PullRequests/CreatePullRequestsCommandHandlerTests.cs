using FluentAssertions;
using FluentAssertions.Equivalency;
using NSubstitute;
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
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = new FileOperations();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, fileOperations, stackConfig);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch), new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
A custom description

{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false),
            [branch2] = new GitHubPullRequest(2, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false)
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
    public async Task WhenCreatingPullRequestsForAStackWithMultipleBranches_EachPullRequestHasTheCorrectStackDescription()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = new FileOperations();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, fileOperations, stackConfig);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch), new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert
        var expectedStackDescription = $@"{StackConstants.StackMarkerStart}
A custom description

{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        gitHubClient.PullRequests.Should().AllSatisfy(pr => pr.Value.Body.Should().Be(expectedStackDescription));
    }

    [Fact]
    public async Task WhenAPullRequestExistForABranch_AndNoneForAnotherBranch_CreatesPullRequestForTheCorrectBranchAndSetsDescription()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubClient = new TestGitHubRepositoryBuilder()
            .WithPullRequest(branch1, pr => pr.WithBody($"{StackConstants.StackMarkerStart} {StackConstants.StackMarkerEnd}"))
            .Build();

        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var fileOperations = new FileOperations();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, fileOperations, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert
        var pullRequestUrls = gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}").ToArray();
        var expectedStackDescription = $@"{StackConstants.StackMarkerStart}
A custom description

{string.Join(Environment.NewLine, pullRequestUrls)}
{StackConstants.StackMarkerEnd}";

        gitHubClient.PullRequests.Should().AllSatisfy(pr => pr.Value.Body.Should().Be(expectedStackDescription));
    }

    [Fact]
    public async Task WhenStackNameIsProvided_PullRequestsAreCreatedForThatStack()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = new FileOperations();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, fileOperations, stackConfig);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch), new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        // Act
        await handler.Handle(new CreatePullRequestsCommandInputs("Stack1"));

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
A custom description

{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false),
            [branch2] = new GitHubPullRequest(2, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false)
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
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = new FileOperations();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, fileOperations, stackConfig);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch), new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
A custom description

{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false),
            [branch2] = new GitHubPullRequest(2, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false)
        };

        gitHubClient.PullRequests.Should().BeEquivalentTo(expectedPullRequests, ExcludeUnimportantPullRequestProperties);
    }

    [Fact]
    public async Task WhenStackNameIsProvided_ButTheStackDoesNotExist_Throws()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = new FileOperations();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, fileOperations, stackConfig);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(h => h.Handle(new CreatePullRequestsCommandInputs(invalidStackName)))
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
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubClient = new TestGitHubRepositoryBuilder()
            .WithPullRequest(branch1, pr => pr
                .WithBody($"{StackConstants.StackMarkerStart} {StackConstants.StackMarkerEnd}")
                .WithTitle("PR Title")
                .Merged())
            .Build();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = new FileOperations();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, fileOperations, stackConfig);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch2, sourceBranch)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
A custom description

{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Merged, Some.HttpsUri(), false),
            [branch2] = new GitHubPullRequest(2, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false)
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
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = new FileOperations();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, fileOperations, stackConfig);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        Directory.CreateDirectory(Path.Join(repo.LocalDirectoryPath, ".github"));
        File.WriteAllText(Path.Join(repo.LocalDirectoryPath, ".github", "PULL_REQUEST_TEMPLATE.md"), "This is the PR template");

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch), new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
A custom description

{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}
This is the PR template";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false),
            [branch2] = new GitHubPullRequest(2, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false)
        };

        gitHubClient.PullRequests.Should().BeEquivalentTo(expectedPullRequests, ExcludeUnimportantPullRequestProperties);
    }

    [Fact]
    public async Task WhenAPullRequestTemplateDoesNotExistInTheRepo_TheStackPrListMarkersArePutIntoTheBody()
    {
        // Arrange
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = new FileOperations();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, fileOperations, stackConfig);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
            
{StackConstants.StackMarkerDescription}

{StackConstants.StackMarkerEnd}";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false)
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
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = new FileOperations();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, fileOperations, stackConfig);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch), new PullRequestCreateAction(branch2, branch1)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Confirm(Questions.CreatePullRequestAsDraft, false).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

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
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubClient = new TestGitHubRepositoryBuilder().Build();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = new FileOperations();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, fileOperations, stackConfig);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider
            .MultiSelect(Questions.SelectPullRequestsToCreate, Arg.Any<PullRequestCreateAction[]>(), Arg.Any<Func<PullRequestCreateAction, string>>())
            .Returns([new PullRequestCreateAction(branch1, sourceBranch)]);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
            
{StackConstants.StackMarkerDescription}

{StackConstants.StackMarkerEnd}";

        var expectedPullRequests = new Dictionary<string, GitHubPullRequest>
        {
            [branch1] = new GitHubPullRequest(1, "PR Title", expectedPrBody, GitHubPullRequestStates.Open, Some.HttpsUri(), false)
        };

        gitHubClient.PullRequests.Should().BeEquivalentTo(expectedPullRequests, ExcludeUnimportantPullRequestProperties);
    }
}

public class TestGitHubPullRequestBuilder
{
    int _number = Some.Int();
    string _title = Some.Name();
    string _body = Some.Name();
    string _state = GitHubPullRequestStates.Open;
    Uri _url = Some.HttpsUri();
    bool _draft = false;

    public TestGitHubPullRequestBuilder WithNumber(int number)
    {
        _number = number;
        return this;
    }

    public TestGitHubPullRequestBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public TestGitHubPullRequestBuilder WithBody(string body)
    {
        _body = body;
        return this;
    }

    public TestGitHubPullRequestBuilder Merged()
    {
        _state = GitHubPullRequestStates.Merged;
        return this;
    }

    public TestGitHubPullRequestBuilder Open()
    {
        _state = GitHubPullRequestStates.Open;
        return this;
    }

    public TestGitHubPullRequestBuilder Closed()
    {
        _state = GitHubPullRequestStates.Closed;
        return this;
    }

    public TestGitHubPullRequestBuilder WithUrl(Uri url)
    {
        _url = url;
        return this;
    }

    public TestGitHubPullRequestBuilder AsDraft()
    {
        _draft = true;
        return this;
    }

    public GitHubPullRequest Build()
    {
        return new GitHubPullRequest(_number, _title, _body, _state, _url, _draft);
    }
}

public class TestGitHubRepositoryBuilder
{
    readonly Dictionary<string, GitHubPullRequest> _pullRequests = new();

    public TestGitHubRepositoryBuilder WithPullRequest(string branch, Action<TestGitHubPullRequestBuilder> pullRequestBuilder)
    {
        var builder = new TestGitHubPullRequestBuilder();
        pullRequestBuilder(builder);
        _pullRequests.Add(branch, builder.Build());
        return this;
    }

    public TestGitHubRepository Build()
    {
        return new TestGitHubRepository(_pullRequests);
    }
}

public class TestGitHubRepository(Dictionary<string, GitHubPullRequest> PullRequests) : IGitHubClient
{
    public Dictionary<string, GitHubPullRequest> PullRequests { get; } = PullRequests;

    public GitHubPullRequest CreatePullRequest(string headBranch, string baseBranch, string title, string bodyFilePath, bool draft)
    {
        var prBody = File.ReadAllText(bodyFilePath).Trim();
        var pr = new GitHubPullRequest(Some.Int(), title, prBody, GitHubPullRequestStates.Open, Some.HttpsUri(), draft);
        PullRequests.Add(headBranch, pr);
        return pr;
    }

    public void EditPullRequest(int number, string body)
    {
        if (!PullRequests.Any(pr => pr.Value.Number == number))
        {
            throw new InvalidOperationException("Pull request not found.");
        }

        var pr = PullRequests.First(p => p.Value.Number == number);
        PullRequests[pr.Key] = pr.Value with { Body = body };
    }

    public GitHubPullRequest? GetPullRequest(string branch)
    {
        return PullRequests.GetValueOrDefault(branch);
    }

    public void OpenPullRequest(GitHubPullRequest pullRequest)
    {
    }
}
