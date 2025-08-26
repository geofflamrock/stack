using System.CommandLine;

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
        IStdOutLogger stdOutLogger,
        IStdErrLogger stdErrLogger,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        OpenPullRequestsCommandHandler handler)
    : base("open", "Open pull requests for a stack in the default browser.", stdOutLogger, stdErrLogger, inputProvider, executionContext)
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
    ILogger logger,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<OpenPullRequestsCommandInputs>
{
    public override async Task Handle(OpenPullRequestsCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            logger.Information("No stacks found for current repository.");
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
            logger.Information($"No pull requests found for stack {stack.Name.Branch()}");
            return;
        }

        foreach (var pullRequest in pullRequestsInStack)
        {
            gitHubClient.OpenPullRequest(pullRequest);
        }
    }
}
