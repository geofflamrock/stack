using FluentAssertions;
using NSubstitute;
using Stack.Commands;
using Stack.Git;
using Stack.Infrastructure.Settings;
using Stack.Tests.Helpers;
using Xunit;

namespace Stack.Tests.Commands.Stack;

public class ListStacksCommandHandlerTests
{
    [Fact]
    public async Task WhenCurrentBranchBelongsToSingleStack_MarksStackAsCurrent()
    {
        var currentBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();
        var firstStackName = "Stack-One";
        var secondStackName = "Stack-Two";

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName(firstStackName)
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(Some.BranchName())
                .WithBranch(branch => branch.WithName(currentBranch)))
            .WithStack(stack => stack
                .WithName(secondStackName)
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(Some.BranchName()))
            .Build();

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var gitClient = Substitute.For<IGitClient>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(currentBranch);

        var handler = new ListStacksCommandHandler(stackConfig, gitClientFactory, executionContext);

        var response = await handler.Handle(new ListStacksCommandInputs(), CancellationToken.None);

        response.Stacks.Should().HaveCount(2);
        response.Stacks.Single(s => s.Name == firstStackName).IsCurrent.Should().BeTrue();
        response.Stacks.Single(s => s.Name == secondStackName).IsCurrent.Should().BeFalse();
    }

    [Fact]
    public async Task WhenCurrentBranchBelongsToMultipleStacks_NoStackMarkedCurrent()
    {
        var currentBranch = Some.BranchName();
        var remoteUri = Some.HttpsUri().ToString();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack-One")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(Some.BranchName())
                .WithBranch(branch => branch.WithName(currentBranch)))
            .WithStack(stack => stack
                .WithName("Stack-Two")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(Some.BranchName())
                .WithBranch(branch => branch.WithName(currentBranch)))
            .Build();

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var gitClient = Substitute.For<IGitClient>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(currentBranch);

        var handler = new ListStacksCommandHandler(stackConfig, gitClientFactory, executionContext);

        var response = await handler.Handle(new ListStacksCommandInputs(), CancellationToken.None);

        response.Stacks.Should().HaveCount(2);
        response.Stacks.Should().OnlyContain(stack => !stack.IsCurrent);
    }

    [Fact]
    public async Task WhenCurrentBranchNotInAnyStack_NoStackMarkedCurrent()
    {
        var remoteUri = Some.HttpsUri().ToString();
        var currentBranch = Some.BranchName();

        var stackConfig = new TestStackConfigBuilder()
            .WithStack(stack => stack
                .WithName("Stack-One")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(Some.BranchName())
                .WithBranch(branch => branch.WithName(Some.BranchName())))
            .WithStack(stack => stack
                .WithName("Stack-Two")
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(Some.BranchName()))
            .Build();

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        var gitClient = Substitute.For<IGitClient>();
        var executionContext = new CliExecutionContext { WorkingDirectory = "/some/path" };

        gitClientFactory.Create(executionContext.WorkingDirectory).Returns(gitClient);
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(currentBranch);

        var handler = new ListStacksCommandHandler(stackConfig, gitClientFactory, executionContext);

        var response = await handler.Handle(new ListStacksCommandInputs(), CancellationToken.None);

        response.Stacks.Should().HaveCount(2);
        response.Stacks.Should().OnlyContain(stack => !stack.IsCurrent);
    }
}
