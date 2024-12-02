using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class OpenPullRequestsCommandHandlerTests
{
    [Fact]
    public async Task WhenThereAreMultiplePullRequestsInAStack_OpensAllPullRequests()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new OpenPullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .GetPullRequest("branch-3")
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .GetPullRequest("branch-5")
            .Returns(prForBranch5);

        // Act
        await handler.Handle(OpenPullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.Received().OpenPullRequest(prForBranch3);
        gitHubOperations.Received().OpenPullRequest(prForBranch5);
    }

    [Fact]
    public async Task WhenThereAreSomePullRequestsInAStack_OpensAllPullRequests()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new OpenPullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<global::Stack.Models.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<global::Stack.Models.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .GetPullRequest("branch-3")
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Closed, Some.HttpsUri());

        // Act
        await handler.Handle(OpenPullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.Received().OpenPullRequest(prForBranch3);
        gitHubOperations.DidNotReceive().OpenPullRequest(prForBranch5);
    }

    [Fact]
    public async Task WhenStackNameIsProvided_OpensAllPullRequestsForTheStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new OpenPullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .GetPullRequest("branch-3")
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .GetPullRequest("branch-5")
            .Returns(prForBranch5);

        // Act
        await handler.Handle(new OpenPullRequestsCommandInputs("Stack1"));

        // Assert        
        gitHubOperations.Received().OpenPullRequest(prForBranch3);
        gitHubOperations.Received().OpenPullRequest(prForBranch5);
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_OpensAllPullRequestsForTheStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new OpenPullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"])
        ]);
        stackConfig.Load().Returns(stacks);

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .GetPullRequest("branch-3")
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .GetPullRequest("branch-5")
            .Returns(prForBranch5);

        // Act
        await handler.Handle(OpenPullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.Received().OpenPullRequest(prForBranch3);
        gitHubOperations.Received().OpenPullRequest(prForBranch5);
    }

    [Fact]
    public async Task WhenStackNameIsProvided_ButItStackDoesNotExist_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new OpenPullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .GetPullRequest("branch-3")
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .GetPullRequest("branch-5")
            .Returns(prForBranch5);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(h => h.Handle(new OpenPullRequestsCommandInputs(invalidStackName)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }
}
