using System.CommandLine;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class OpenPullRequestsCommand : Command
{
    public OpenPullRequestsCommand() : base("open", "Open pull requests for a stack in the default browser.")
    {
        Add(CommonOptions.Stack);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var handler = new OpenPullRequestsCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, new GitClientSettings(Verbose, WorkingDirectory)),
            new CachingGitHubClient(new GitHubClient(StdErrLogger, new GitHubClientSettings(Verbose, WorkingDirectory))),
            new FileStackConfig());

        await handler.Handle(new OpenPullRequestsCommandInputs(
            parseResult.GetValue(CommonOptions.Stack)));
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
    public override async Task Handle(OpenPullRequestsCommandInputs inputs)
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
        var stack = inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch);

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