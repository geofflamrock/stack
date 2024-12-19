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
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, fileOperations, stackConfig);

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
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(2)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), false)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest(branch2, branch1, "PR Title", Arg.Any<string>(), false)
            .Returns(prForBranch5);

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.Received().CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), false);
        gitHubOperations.Received().CreatePullRequest(branch2, branch1, "PR Title", Arg.Any<string>(), false);
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

        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, fileOperations, stackConfig);

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
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(2)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), false)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest(branch2, branch1, "PR Title", Arg.Any<string>(), false)
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
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, fileOperations, stackConfig);

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
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(1)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations.GetPullRequest(branch1).Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest(branch2, branch1, "PR Title", Arg.Any<string>(), false)
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
        gitHubOperations.DidNotReceive().CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), false);
        gitHubOperations.Received().CreatePullRequest(branch2, branch1, "PR Title", Arg.Any<string>(), false);
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
        var sourceBranch = Some.BranchName();
        var branch1 = Some.BranchName();
        var branch2 = Some.BranchName();
        using var repo = new TestGitRepositoryBuilder()
            .WithBranch(sourceBranch, true)
            .WithBranch(branch1, true)
            .WithBranch(branch2, true)
            .Build();

        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, fileOperations, stackConfig);

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
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(2)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), false)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest(branch2, branch1, "PR Title", Arg.Any<string>(), false)
            .Returns(prForBranch5);

        // Act
        await handler.Handle(new CreatePullRequestsCommandInputs("Stack1"));

        // Assert        
        gitHubOperations.Received().CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), false);
        gitHubOperations.Received().CreatePullRequest(branch2, branch1, "PR Title", Arg.Any<string>(), false);
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

        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, fileOperations, stackConfig);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        var stacks = new List<Config.Stack>(
        [
            new("Stack1", repo.RemoteUri, sourceBranch, [branch1, branch2])
        ]);
        stackConfig.Load().Returns(stacks);

        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(2)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), false)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest(branch2, branch1, "PR Title", Arg.Any<string>(), false)
            .Returns(prForBranch5);

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.Received().CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), false);
        gitHubOperations.Received().CreatePullRequest(branch2, branch1, "PR Title", Arg.Any<string>(), false);
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

        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, fileOperations, stackConfig);

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

        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, fileOperations, stackConfig);

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
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(1)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");
        inputProvider.Text(Questions.PullRequestStackDescription, Arg.Any<string>()).Returns("A custom description");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Merged, Some.HttpsUri(), false);
        gitHubOperations.GetPullRequest(branch1).Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest(branch2, sourceBranch, "PR Title", Arg.Any<string>(), false)
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
        gitHubOperations.DidNotReceive().CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), false);
        gitHubOperations.Received().CreatePullRequest(branch2, sourceBranch, "PR Title", Arg.Any<string>(), false);
        var expectedStackDescription = $@"<!-- stack-pr-list -->
A custom description

- {prForBranch3.Url}
- {prForBranch5.Url}
<!-- /stack-pr-list -->";

        prForBranch3.Body.Should().Be(expectedStackDescription);
        prForBranch5.Body.Should().Be(expectedStackDescription);
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

        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, fileOperations, stackConfig);

        outputProvider
            .WhenForAnyArgs(o => o.Status(Arg.Any<string>(), Arg.Any<Action>()))
            .Do(ci => ci.ArgAt<Action>(1)());

        fileOperations.Exists(Arg.Any<string>()).Returns(true);

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
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(1)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");

        var prForBranch1 = new GitHubPullRequest(1, "PR Title", "PR Template", GitHubPullRequestStates.Open, Some.HttpsUri(), false);
        gitHubOperations
            .CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), false)
            .Returns(prForBranch1);

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.Received().CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), false);
        fileOperations.Received().Copy(Arg.Any<string>(), Arg.Any<string>(), true);
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

        var gitHubOperations = Substitute.For<IGitHubOperations>();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = Substitute.For<IOutputProvider>();
        var fileOperations = Substitute.For<IFileOperations>();
        var gitOperations = new GitOperations(outputProvider, repo.GitOperationSettings);
        var handler = new CreatePullRequestsCommandHandler(inputProvider, outputProvider, gitOperations, gitHubOperations, fileOperations, stackConfig);

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
        inputProvider.Confirm(Questions.ConfirmStartCreatePullRequests(2)).Returns(true);
        inputProvider.Confirm(Questions.ConfirmCreatePullRequests).Returns(true);
        inputProvider.Confirm(Questions.CreatePullRequestAsDraft, false).Returns(true);
        inputProvider.Text(Questions.PullRequestTitle).Returns("PR Title");

        var prForBranch3 = new GitHubPullRequest(1, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), true);
        gitHubOperations
            .CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), true)
            .Returns(prForBranch3);

        var prForBranch5 = new GitHubPullRequest(2, "PR Title", string.Empty, GitHubPullRequestStates.Open, Some.HttpsUri(), true);
        gitHubOperations
            .CreatePullRequest(branch2, branch1, "PR Title", Arg.Any<string>(), true)
            .Returns(prForBranch5);

        // Act
        await handler.Handle(CreatePullRequestsCommandInputs.Empty);

        // Assert        
        gitHubOperations.Received().CreatePullRequest(branch1, sourceBranch, "PR Title", Arg.Any<string>(), true);
        gitHubOperations.Received().CreatePullRequest(branch2, branch1, "PR Title", Arg.Any<string>(), true);
    }
}
