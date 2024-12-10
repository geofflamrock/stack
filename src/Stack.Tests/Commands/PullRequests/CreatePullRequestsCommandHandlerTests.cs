using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Tests.Helpers;

namespace Stack.Tests.Commands.PullRequests;

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
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(2)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-3", "branch-1")).Returns("PR Title for branch-3");
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-3")).Returns("PR Title for branch-5");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, false)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, false)
            .Returns(prForBranch5);

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.Received().CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, false);
        gitHubOperations.Received().CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, false);
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
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(2)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-3", "branch-1")).Returns("PR Title for branch-3");
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-3")).Returns("PR Title for branch-5");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, false)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, false)
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
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations
             .GetBranchesThatExistInRemote(Arg.Any<string[]>())
             .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(1)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-3")).Returns("PR Title for branch-5");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations.GetPullRequest("branch-3").Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, false)
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
        gitHubOperations.DidNotReceive().CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, false);
        gitHubOperations.Received().CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, false);
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
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(2)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-3", "branch-1")).Returns("PR Title for branch-3");
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-3")).Returns("PR Title for branch-5");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, false)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, false)
            .Returns(prForBranch5);

        // Act
        await handler.Handle(new CreatePullRequestsCommandInputs("Stack1", null));

        // Assert        
        gitHubOperations.Received().CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, false);
        gitHubOperations.Received().CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, false);
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
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(2)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-3", "branch-1")).Returns("PR Title for branch-3");
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-3")).Returns("PR Title for branch-5");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, false)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, false)
            .Returns(prForBranch5);

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.Received().CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, false);
        gitHubOperations.Received().CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, false);
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

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);

        // Act and assert
        var invalidStackName = Some.Name();
        await handler.Invoking(h => h.Handle(new CreatePullRequestsCommandInputs(invalidStackName, null)))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"Stack '{invalidStackName}' not found.");
    }

    [Fact]
    public async Task WhenAPullRequestExistForABranch_AndHasBeenMerged_AndNoneForAnotherBranch_CreatesPullRequestTargetingTheCorrectBranchAndSetsDescription()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(1)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-1")).Returns("PR Title for branch-5");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Merged, Some.HttpsUri(), false);
        gitHubOperations.GetPullRequest("branch-3").Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-1", "PR Title for branch-5", string.Empty, false)
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
        gitHubOperations.DidNotReceive().CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, false);
        gitHubOperations.Received().CreatePullRequest("branch-5", "branch-1", "PR Title for branch-5", string.Empty, false);
        var expectedStackDescription = $@"<!-- stack-pr-list -->
A custom description

- {prForBranch3.Url}
- {prForBranch5.Url}
<!-- /stack-pr-list -->";

        prForBranch3.Body.Should().Be(expectedStackDescription);
        prForBranch5.Body.Should().Be(expectedStackDescription);
    }

    [Fact]
    public async Task WhenAskedWhetherToCreateAPullRequestAsADraft_AndTheAnswerIsYes_PullRequestsCreatedAsADraft()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(2)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Confirm(Questions.CreatePullRequestsAsDrafts, false).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-3", "branch-1")).Returns("PR Title for branch-3");
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-3")).Returns("PR Title for branch-5");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), true);
        gitHubOperations
            .CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, true)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), true);
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, true)
            .Returns(prForBranch5);

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.Received().CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, true);
        gitHubOperations.Received().CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, true);
    }

    [Fact]
    public async Task WhenDraftIsProvided_PullRequestsAreCreatedAsDrafts()
    {
        // Arrange
        var gitOperations = Substitute.For<IGitOperations>();
        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, stackConfig);

        var remoteUri = Some.HttpsUri().ToString();
        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        gitOperations.GetRemoteUri().Returns(remoteUri);
        gitOperations.GetCurrentBranch().Returns("branch-1");
        gitOperations
            .GetBranchesThatExistInRemote(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        gitOperations
            .GetBranchesThatExistLocally(Arg.Any<string[]>())
            .Returns(["branch-1", "branch-3", "branch-5"]);

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", remoteUri, "branch-1", ["branch-3", "branch-5"]),
            new("Stack2", remoteUri, "branch-2", ["branch-4"])
        ]);
        stackConfig.Load().Returns(stacks);
        stackConfig
            .WhenForAnyArgs(s => s.Save(Arg.Any<List<Config.Stack>>()))
            .Do(ci => stacks = ci.ArgAt<List<Config.Stack>>(0));

        inputProvider.Select(Questions.SelectStack, Arg.Any<string[]>()).Returns("Stack1");
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(2)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle("branch-3", "branch-1")).Returns("PR Title for branch-3");
        inputProvider.Text(Questions.PullRequestTitle("branch-5", "branch-3")).Returns("PR Title for branch-5");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title for branch-3", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), true);
        gitHubOperations
            .CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, true)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title for branch-5", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), true);
        gitHubOperations
            .CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, true)
            .Returns(prForBranch5);

        // Act
        await handler.Handle(new CreatePullRequestsCommandInputs(null, true));

        // Assert        
        gitHubOperations.Received().CreatePullRequest("branch-3", "branch-1", "PR Title for branch-3", string.Empty, true);
        gitHubOperations.Received().CreatePullRequest("branch-5", "branch-3", "PR Title for branch-5", string.Empty, true);
        inputProvider.DidNotReceive().Confirm(Questions.CreatePullRequestsAsDrafts, false);
    }
}
