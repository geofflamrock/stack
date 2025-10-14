using System.CommandLine;
using Microsoft.Extensions.Logging;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class OpenPullRequestsCommand : Command
{
    private readonly OpenPullRequestsCommandHandler handler;

    public OpenPullRequestsCommand(
        OpenPullRequestsCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<OpenPullRequestsCommand> logger)
        : base("open", "Open pull requests for a stack in the default browser.", executionContext, inputProvider, outputProvider, logger)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new OpenPullRequestsCommandInputs(
                parseResult.GetValue(CommonOptions.Stack)),
            cancellationToken);
    }
}

public record OpenPullRequestsCommandInputs(string? Stack)
{
    public static OpenPullRequestsCommandInputs Empty => new((string?)null);
}

public class OpenPullRequestsCommandHandler(
    IInputProvider inputProvider,
    ILogger<OpenPullRequestsCommandHandler> logger,
    IGitClientFactory gitClientFactory,
    CliExecutionContext executionContext,
    IGitHubClient gitHubClient,
    IStackRepository repository)
    : CommandHandlerBase<OpenPullRequestsCommandInputs>
{
    public override async Task Handle(OpenPullRequestsCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        var stacksForRemote = repository.GetStacks();

        if (stacksForRemote.Count == 0)
        {
            logger.NoStacksForRepository();
            return;
        }

        var currentBranch = gitClient.GetCurrentBranch();
        var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
        }

        var pullRequestsInStack = new List<GitHubPullRequest>();

        foreach (var branch in stack.Branches)
        {
            var existingPullRequest = gitHubClient.GetPullRequest(branch.Name);

            if (existingPullRequest is not null && existingPullRequest.State != GitHubPullRequestStates.Closed)
            {
                pullRequestsInStack.Add(existingPullRequest);
            }
        }

        if (pullRequestsInStack.Count == 0)
        {
            logger.NoPullRequestsForStack(stack.Name);
            return;
        }

        foreach (var pullRequest in pullRequestsInStack)
        {
            gitHubClient.OpenPullRequest(pullRequest);
        }
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Information, Message = "No pull requests found for stack \"{Stack}\"")]
    public static partial void NoPullRequestsForStack(this ILogger logger, string stack);
}
