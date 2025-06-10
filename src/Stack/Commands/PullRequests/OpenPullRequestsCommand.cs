using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class OpenPullRequestsCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to open PRs for.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }
}

public class OpenPullRequestsCommand : Command<OpenPullRequestsCommandSettings>
{
    protected override async Task Execute(OpenPullRequestsCommandSettings settings)
    {
        var handler = new OpenPullRequestsCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new GitHubClient(StdErrLogger, settings.GetGitHubClientSettings()),
            new FileStackConfig());

        await handler.Handle(new OpenPullRequestsCommandInputs(settings.Stack));
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