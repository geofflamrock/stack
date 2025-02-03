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

public class SetPullRequestLabelsCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task WhenPullRequestsExistInAStack_WithNoLabels_TheNewLabelsAreApplied()
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
            .WithPullRequest(branch1)
            .WithPullRequest(branch2)
            .WithLabel("label1")
            .WithLabel("label2")
            .Build();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new SetPullRequestLabelsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

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
        inputProvider.MultiSelect(Questions.PullRequestLabels, Arg.Any<string[]>(), Arg.Any<bool>(), Arg.Any<string[]>()).Returns(["label1", "label2"]);

        // Act
        await handler.Handle(SetPullRequestLabelsCommandInputs.Empty);

        // Assert
        gitHubClient.PullRequests.Should().AllSatisfy(pr => pr.Value.LabelNames.Should().BeEquivalentTo(["label1", "label2"]));
    }

    [Fact]
    public async Task WhenPullRequestsOnlyExistForSomeBranchesInAStack_LabelsAreOnlyAppliedToThosePrs()
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
            .WithPullRequest(branch1)
            .WithLabel("label1")
            .WithLabel("label2")
            .Build();
        var stackConfig = Substitute.For<IStackConfig>();
        var inputProvider = Substitute.For<IInputProvider>();
        var outputProvider = new TestOutputProvider(testOutputHelper);
        var gitClient = new GitClient(outputProvider, repo.GitClientSettings);
        var handler = new SetPullRequestLabelsCommandHandler(inputProvider, outputProvider, gitClient, gitHubClient, stackConfig);

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
        inputProvider.MultiSelect(Questions.PullRequestLabels, Arg.Any<string[]>(), Arg.Any<bool>(), Arg.Any<string[]>()).Returns(["label1", "label2"]);

        // Act
        await handler.Handle(SetPullRequestLabelsCommandInputs.Empty);

        // Assert
        gitHubClient.PullRequests.Should().ContainKey(branch1);
        gitHubClient.PullRequests.Should().AllSatisfy(pr => pr.Value.LabelNames.Should().BeEquivalentTo(["label1", "label2"]));
    }
}
