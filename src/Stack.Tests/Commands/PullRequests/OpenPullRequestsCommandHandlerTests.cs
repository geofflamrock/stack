using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.PullRequests;

public class OpenPullRequestsCommandHandlerTests
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
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new OpenPullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

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

        var prForBranch1 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubClient
            .GetPullRequest(branch1)
            .Returns(prForBranch1);

        var prForBranch2 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
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
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new OpenPullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

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

        var prForBranch1 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubClient
            .GetPullRequest(branch1)
            .Returns(prForBranch1);

        var prForBranch2 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Closed, Some.HttpsUri(), false);

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
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new OpenPullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);

        var prForBranch1 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubClient
            .GetPullRequest(branch1)
            .Returns(prForBranch1);

        var prForBranch2 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
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
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new OpenPullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2])
        ]);
        stackConfig.Load().Returns(stacks);

        var prForBranch1 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubClient
            .GetPullRequest(branch1)
            .Returns(prForBranch1);

        var prForBranch2 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
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
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new OpenPullRequestsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2]),
            new("Stack2", repo.RemoteUri, sourceBranch, [])
        ]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(h => h.Handle(new OpenPullRequestsCommandInputs(invalidStackName)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }
}
