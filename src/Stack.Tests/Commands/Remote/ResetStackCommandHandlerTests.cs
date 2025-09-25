using FluentAssertions;
using Meziantou.Extensions.Logging.Xunit;
using NSubstitute;
using Stack.Commands;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Tests.Helpers;
using Xunit.Abstractions;

namespace Stack.Tests.Commands.Remote;

public class ResetStackCommandHandlerTests(ITestOutputHelper testOutputHelper)
{
    const string StackName = "Stack1";

    [Fact]
    public async Task WhenConfirmationIsDeclined_DoesNotResetStack()
    {
        // Arrange
        var context = CreateContext();
        context.InputProvider
            .Confirm(Questions.ConfirmResetStack, Arg.Any<CancellationToken>(), false)
            .Returns(Task.FromResult(false));

        // Act
        await context.Handler.Handle(new ResetStackCommandInputs(StackName, false), CancellationToken.None);

        // Assert
        await context.StackActions.DidNotReceive().ResetStack(Arg.Any<Config.Stack>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenConfirmationIsAccepted_ResetsStack()
    {
        // Arrange
        var context = CreateContext();
        context.InputProvider
            .Confirm(Questions.ConfirmResetStack, Arg.Any<CancellationToken>(), false)
            .Returns(Task.FromResult(true));

        // Act
        await context.Handler.Handle(new ResetStackCommandInputs(StackName, false), CancellationToken.None);

        // Assert
        await context.StackActions.Received(1).ResetStack(Arg.Is<Config.Stack>(s => s.Name == StackName), Arg.Any<CancellationToken>());
        context.GitClient.Received(1).ChangeBranch(context.CurrentBranch);
        await context.OutputProvider.Received().WriteLine(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenConfirmOptionProvided_SkipsPromptAndResetsStack()
    {
        // Arrange
        var context = CreateContext();

        // Act
        await context.Handler.Handle(new ResetStackCommandInputs(StackName, true), CancellationToken.None);

        // Assert
        await context.StackActions.Received(1).ResetStack(Arg.Is<Config.Stack>(s => s.Name == StackName), Arg.Any<CancellationToken>());
        await context.InputProvider.DidNotReceive().Confirm(Questions.ConfirmResetStack, Arg.Any<CancellationToken>(), Arg.Any<bool>());
        await context.OutputProvider.DidNotReceive().WriteLine(Arg.Any<string>(), Arg.Any<CancellationToken>());
        context.GitClient.Received(1).ChangeBranch(context.CurrentBranch);
    }

    [Fact]
    public async Task WhenStackNameNotProvided_WithMultipleStacks_AsksForSelection()
    {
        // Arrange
        var otherStackName = "Stack2";
        var context = CreateContext((builder, remoteUri) =>
        {
            var stack1Source = Some.BranchName();
            var stack1Branch = Some.BranchName();
            builder.WithStack(stack => stack
                .WithName(StackName)
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(stack1Source)
                .WithBranch(b => b.WithName(stack1Branch)));

            var stack2Source = Some.BranchName();
            builder.WithStack(stack => stack
                .WithName(otherStackName)
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(stack2Source));
        }, currentBranchOverride: Some.BranchName());

        context.InputProvider
            .Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(StackName));
        context.InputProvider
            .Confirm(Questions.ConfirmResetStack, Arg.Any<CancellationToken>(), false)
            .Returns(Task.FromResult(true));

        // Act
        await context.Handler.Handle(new ResetStackCommandInputs(null, false), CancellationToken.None);

        // Assert
        await context.InputProvider.Received(1).Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        await context.StackActions.Received(1).ResetStack(Arg.Is<Config.Stack>(s => s.Name == StackName), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenStackNameNotProvided_WithSingleStack_DoesNotPromptForSelection()
    {
        // Arrange
        var context = CreateContext();
        context.InputProvider
            .Confirm(Questions.ConfirmResetStack, Arg.Any<CancellationToken>(), false)
            .Returns(Task.FromResult(true));

        // Act
        await context.Handler.Handle(new ResetStackCommandInputs(null, false), CancellationToken.None);

        // Assert
        await context.InputProvider.DidNotReceive().Select(Questions.SelectStack, Arg.Any<string[]>(), Arg.Any<CancellationToken>());
        await context.StackActions.Received(1).ResetStack(Arg.Is<Config.Stack>(s => s.Name == StackName), Arg.Any<CancellationToken>());
    }

    private ResetStackCommandHandlerTestContext CreateContext(
        Action<TestStackConfigBuilder, string>? configureStacks = null,
        string? currentBranchOverride = null)
    {
        var remoteUri = Some.HttpsUri().ToString();
        var stackConfigBuilder = new TestStackConfigBuilder();

        if (configureStacks is null)
        {
            var sourceBranch = Some.BranchName();
            var featureBranch = Some.BranchName();
            stackConfigBuilder.WithStack(stack => stack
                .WithName(StackName)
                .WithRemoteUri(remoteUri)
                .WithSourceBranch(sourceBranch)
                .WithBranch(b => b.WithName(featureBranch)));
        }
        else
        {
            configureStacks(stackConfigBuilder, remoteUri);
        }

        var stackConfig = stackConfigBuilder.Build();
        var stacks = stackConfig.Load().Stacks;
        var currentBranch = currentBranchOverride ?? stacks.First().SourceBranch;

        var inputProvider = Substitute.For<IInputProvider>();
        var logger = XUnitLogger.CreateLogger<ResetStackCommandHandler>(testOutputHelper);
        var displayProvider = new TestDisplayProvider(testOutputHelper);
        var outputProvider = Substitute.For<IOutputProvider>();
        outputProvider.WriteLine(default!, default).ReturnsForAnyArgs(Task.CompletedTask);
        outputProvider.WriteMessage(default!, default).ReturnsForAnyArgs(Task.CompletedTask);
        outputProvider.WriteHeader(default!, default).ReturnsForAnyArgs(Task.CompletedTask);
        outputProvider.WriteNewLine(default).ReturnsForAnyArgs(Task.CompletedTask);

        var gitClient = Substitute.For<IGitClient>();
        gitClient.GetRemoteUri().Returns(remoteUri);
        gitClient.GetCurrentBranch().Returns(currentBranch);

        gitClient.GetBranchStatuses(Arg.Any<string[]>()).Returns(callInfo =>
        {
            var dictionary = new Dictionary<string, GitBranchStatus>();
            foreach (var stack in stacks)
            {
                dictionary[stack.SourceBranch] = CreateBranchStatus(stack.SourceBranch, stack.SourceBranch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase));
                foreach (var branch in stack.AllBranchNames)
                {
                    dictionary[branch] = CreateBranchStatus(branch, branch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase));
                }
            }

            return dictionary;
        });
        gitClient.CompareBranches(default!, default!).ReturnsForAnyArgs((0, 0));

        var gitClientFactory = Substitute.For<IGitClientFactory>();
        gitClientFactory.Create(Arg.Any<string>()).Returns(gitClient);

        var executionContext = new CliExecutionContext { WorkingDirectory = "/repo" };
        var gitHubClient = Substitute.For<IGitHubClient>();
        var stackActions = Substitute.For<IStackActions>();
        stackActions.ResetStack(Arg.Any<Config.Stack>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var handler = new ResetStackCommandHandler(
            inputProvider,
            logger,
            displayProvider,
            outputProvider,
            gitClientFactory,
            executionContext,
            stackConfig,
            gitHubClient,
            stackActions);

        var defaultStack = stacks.FirstOrDefault(s => s.Name.Equals(StackName, StringComparison.OrdinalIgnoreCase)) ?? stacks.First();

        return new ResetStackCommandHandlerTestContext(
            handler,
            inputProvider,
            outputProvider,
            gitClient,
            stackActions,
            stacks,
            defaultStack,
            currentBranch);
    }

    private static GitBranchStatus CreateBranchStatus(string branchName, bool isCurrent)
    {
        return new GitBranchStatus(
            branchName,
            $"origin/{branchName}",
            true,
            isCurrent,
            0,
            0,
            new Commit(Some.Sha(), "test"));
    }

    private sealed record ResetStackCommandHandlerTestContext(
        ResetStackCommandHandler Handler,
        IInputProvider InputProvider,
        IOutputProvider OutputProvider,
        IGitClient GitClient,
        IStackActions StackActions,
        IReadOnlyList<Config.Stack> Stacks,
        Config.Stack PrimaryStack,
        string CurrentBranch);
}
