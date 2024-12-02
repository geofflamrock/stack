using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.Stack;

public class CreatePullRequestsCommandHandlerTests
{
    [Fact]
    public async Task WhenNoPullRequestsExistForAStackWithMultipleBranches_CreatesPullRequestForEachBranchTargetingTheCorrectParentBranch()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesRemoteBranchExist("branch-3").Returns(true);
        gitOperations.DoesRemoteBranchExist("branch-5").Returns(true);

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
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-3", "branch-1")).Returns("PR Title for branch-3");
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-3")).Returns("PR Title for branch-5");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty)
            .Returns(prForBranch5);

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.Received().CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty);
        gitHubOperations.Received().CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty);
    }

    [Fact]
    public async Task WhenCreatingPullRequestsForAStackWithMultipleBranches_EachPullRequestHasTheCorrectStackDescription()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesRemoteBranchExist("branch-3").Returns(true);
        gitOperations.DoesRemoteBranchExist("branch-5").Returns(true);

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
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-3", "branch-1")).Returns("PR Title for branch-3");
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-3")).Returns("PR Title for branch-5");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty)
            .Returns(prForBranch5);

        gitHubOperations
            .When(g => g.EditPullRequest(1, Arg.Any<string>()))
            .Do(ci => prForBranch3 = prForBranch3 with { Body = ci.ArgAt<string>(1) });

        gitHubOperations
            .When(g => g.EditPullRequest(2, Arg.Any<string>()))
            .Do(ci => prForBranch5 = prForBranch5 with { Body = ci.ArgAt<string>(1) });

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert
        var expectedStackDescription = $@"<!-- stack-pr-list -->
A custom description

- {prForBranch3.Url}
- {prForBranch5.Url}
<!-- /stack-pr-list -->";

        prForBranch3.Body.Should().Be(expectedStackDescription);
        prForBranch5.Body.Should().Be(expectedStackDescription);
    }

    [Fact]
    public async Task WhenAPullRequestExistForABranch_AndNoneForAnotherBranch_CreatesPullRequestForTheCorrectBranchAndSetsDescription()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesRemoteBranchExist("branch-3").Returns(true);
        gitOperations.DoesRemoteBranchExist("branch-5").Returns(true);

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
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-3")).Returns("PR Title for branch-5");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations.GetPullRequest("branch-3").Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty)
            .Returns(prForBranch5);

        gitHubOperations
            .When(g => g.EditPullRequest(1, Arg.Any<string>()))
            .Do(ci => prForBranch3 = prForBranch3 with { Body = ci.ArgAt<string>(1) });

        gitHubOperations
            .When(g => g.EditPullRequest(2, Arg.Any<string>()))
            .Do(ci => prForBranch5 = prForBranch5 with { Body = ci.ArgAt<string>(1) });

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.DidNotReceive().CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty);
        gitHubOperations.Received().CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty);
        var expectedStackDescription = $@"<!-- stack-pr-list -->
A custom description

- {prForBranch3.Url}
- {prForBranch5.Url}
<!-- /stack-pr-list -->";

        prForBranch3.Body.Should().Be(expectedStackDescription);
        prForBranch5.Body.Should().Be(expectedStackDescription);
    }

    [Fact]
    public async Task WhenStackNameIsProvided_PullRequestsAreCreatedForThatStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesRemoteBranchExist("branch-3").Returns(true);
        gitOperations.DoesRemoteBranchExist("branch-5").Returns(true);

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
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-3", "branch-1")).Returns("PR Title for branch-3");
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-3")).Returns("PR Title for branch-5");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty)
            .Returns(prForBranch5);

        // Act
        await handler.Handle(new CreatePullRequestsCommandInputs("Stack1"));

        // Assert        
        gitHubOperations.Received().CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty);
        gitHubOperations.Received().CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty);
    }

    [Fact]
    public async Task WhenOnlyOneStackExists_DoesNotAskForStackName_PullRequestsAreCreatedForThatStack()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations.DoesRemoteBranchExist("branch-3").Returns(true);
        gitOperations.DoesRemoteBranchExist("branch-5").Returns(true);

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-3", "branch-1")).Returns("PR Title for branch-3");
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-3")).Returns("PR Title for branch-5");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri());
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty)
            .Returns(prForBranch5);

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.Received().CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty);
        gitHubOperations.Received().CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty);
    }

    [Fact]
    public async Task WhenStackNameIsProvided_ButTheStackDoesNotExist_Throws()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");

        var stacks = new List<global::Stack.Models.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(h => h.Handle(new CreatePullRequestsCommandInputs(invalidStackName)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }
}
