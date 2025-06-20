using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.PullRequests;

public class SetPullRequestDescriptionCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenPullRequestsExistInAStack_TheNewDescriptionIsAppliedCorrectlyAlongWithTheListOfPrs()
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
            .WithPullRequest(branch1, pr => pr.WithBody($"{StackConstants.StackMarkerStart} The current description {StackConstants.StackMarkerEnd}"))
            .WithPullRequest(branch2, pr => pr.WithBody($"{StackConstants.StackMarkerStart} The current description {StackConstants.StackMarkerEnd}"))
            .Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new SetPullRequestDescriptionCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        // Act
        await handler.Handle(SetPullRequestDescriptionCommandInputs.Empty);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
A custom description

{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        gitHubClient.PullRequests.Should().AllSatisfy(pr => pr.Value.Body.Should().Be(expectedPrBody));
    }

    [Fact]
    public async Task WhenPullRequestsOnlyExistForSomeBranchesInAStack_TheNewDescriptionIsAppliedCorrectlyAlongWithTheListOfPrs()
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
            .WithPullRequest(branch1, pr => pr.WithBody($"{StackConstants.StackMarkerStart} The current description {StackConstants.StackMarkerEnd}"))
            .Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new SetPullRequestDescriptionCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        // Act
        await handler.Handle(SetPullRequestDescriptionCommandInputs.Empty);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
A custom description

{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        gitHubClient.PullRequests.Should().ContainSingle();
        gitHubClient.PullRequests.Should().AllSatisfy(pr => pr.Value.Body.Should().Be(expectedPrBody));
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
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .WithStack(stack => stack
                .WithName("Stack2")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new SetPullRequestDescriptionCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(h => h.Handle(new SetPullRequestDescriptionCommandInputs(invalidStackName)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_TheNewDescriptionIsAppliedCorrectlyAlongWithTheListOfPrs()
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
            .WithPullRequest(branch1, pr => pr.WithBody($"{StackConstants.StackMarkerStart} The current description {StackConstants.StackMarkerEnd}"))
            .WithPullRequest(branch2, pr => pr.WithBody($"{StackConstants.StackMarkerStart} The current description {StackConstants.StackMarkerEnd}"))
            .Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new SetPullRequestDescriptionCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        // Act
        await handler.Handle(SetPullRequestDescriptionCommandInputs.Empty);

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
A custom description

{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        gitHubClient.PullRequests.Should().AllSatisfy(pr => pr.Value.Body.Should().Be(expectedPrBody));
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }

    [Fact]
    public async Task WhenStackNameIsProvided_DoesNotAskForStackName_TheNewDescriptionIsAppliedCorrectlyAlongWithTheListOfPrs()
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
            .WithPullRequest(branch1, pr => pr.WithBody($"{StackConstants.StackMarkerStart} The current description {StackConstants.StackMarkerEnd}"))
            .WithPullRequest(branch2, pr => pr.WithBody($"{StackConstants.StackMarkerStart} The current description {StackConstants.StackMarkerEnd}"))
            .Build();
        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack1")
                .WithRemoteUri(repo.RemoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(branch => branch.WithName(branch1))
                .WithBranch(branch => branch.WithName(branch2)))
            .Build();
        var inputProvider = Substitute.For<IInputProvider>();
        var logger = new TestLogger(testOutputHelper);
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new SetPullRequestDescriptionCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);


        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        // Act
        await handler.Handle(new SetPullRequestDescriptionCommandInputs("Stack1"));

        // Assert
        var expectedPrBody = $@"{StackConstants.StackMarkerStart}
A custom description

{string.Join(Environment.NewLine, gitHubClient.PullRequests.Values.Select(pr => $"- {pr.Url}"))}
{StackConstants.StackMarkerEnd}";

        gitHubClient.PullRequests.Should().AllSatisfy(pr => pr.Value.Body.Should().Be(expectedPrBody));
        inputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>());
    }
}
