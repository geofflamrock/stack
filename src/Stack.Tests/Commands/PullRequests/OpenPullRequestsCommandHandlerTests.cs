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

public class OpenPullRequestsCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenThereAreMultiplePullRequestsInAStack_OpensAllPullRequests()
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

        var gitHubClient = Substitute.For<IGitHubClient>();
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
        var fileOperations = Substitute.For<IFileOperations>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new OpenPullRequestsCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        var prForBranch1 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1);
        gitHubClient
            .GetPullRequest(branch1)
            .Returns(prForBranch1);

        var prForBranch2 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch2);
        gitHubClient
            .GetPullRequest(branch2)
            .Returns(prForBranch2);

        // Act
        await handler.Handle(OpenPullRequestsCommandInputs.Empty);

        // Assert        
        gitHubClient.Received().OpenPullRequest(prForBranch1);
        gitHubClient.Received().OpenPullRequest(prForBranch2);
    }

    [Fact]
    public async Task WhenThereAreSomePullRequestsInAStack_OpensAllPullRequests()
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

        var gitHubClient = Substitute.For<IGitHubClient>();
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
        var fileOperations = Substitute.For<IFileOperations>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new OpenPullRequestsCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        var prForBranch1 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1);
        gitHubClient
            .GetPullRequest(branch1)
            .Returns(prForBranch1);

        var prForBranch2 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Closed, Some.HttpsUri(), false, branch2);

        // Act
        await handler.Handle(OpenPullRequestsCommandInputs.Empty);

        // Assert        
        gitHubClient.Received().OpenPullRequest(prForBranch1);
        gitHubClient.DidNotReceive().OpenPullRequest(prForBranch2);
    }

    [Fact]
    public async Task WhenStackNameIsProvided_OpensAllPullRequestsForTheStack()
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

        var gitHubClient = Substitute.For<IGitHubClient>();
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
        var fileOperations = Substitute.For<IFileOperations>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new OpenPullRequestsCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var prForBranch1 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1);
        gitHubClient
            .GetPullRequest(branch1)
            .Returns(prForBranch1);

        var prForBranch2 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch2);
        gitHubClient
            .GetPullRequest(branch2)
            .Returns(prForBranch2);

        // Act
        await handler.Handle(new OpenPullRequestsCommandInputs("Stack1"));

        // Assert        
        gitHubClient.Received().OpenPullRequest(prForBranch1);
        gitHubClient.Received().OpenPullRequest(prForBranch2);
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_OpensAllPullRequestsForTheStack()
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

        var gitHubClient = Substitute.For<IGitHubClient>();
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
        var fileOperations = Substitute.For<IFileOperations>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new OpenPullRequestsCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        var prForBranch1 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch1);
        gitHubClient
            .GetPullRequest(branch1)
            .Returns(prForBranch1);

        var prForBranch2 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false, branch2);
        gitHubClient
            .GetPullRequest(branch2)
            .Returns(prForBranch2);

        // Act
        await handler.Handle(OpenPullRequestsCommandInputs.Empty);

        // Assert        
        gitHubClient.Received().OpenPullRequest(prForBranch1);
        gitHubClient.Received().OpenPullRequest(prForBranch2);
    }

    [Fact]
    public async Task WhenStackNameIsProvided_ButItStackDoesNotExist_Throws()
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

        var gitHubClient = Substitute.For<IGitHubClient>();
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
        var fileOperations = Substitute.For<IFileOperations>();
        var gitClient = new GitClient(logger, repo.GitClientSettings);
        var handler = new OpenPullRequestsCommandHandler(inputProvider, logger, gitClient, gitHubClient, stackConfig);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(h => h.Handle(new OpenPullRequestsCommandInputs(invalidStackName)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }
}
