using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class SetPullRequestDescriptionCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to open PRs for.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }
}

public class SetPullRequestDescriptionCommand : Command<SetPullRequestDescriptionCommandSettings>
{
    protected override async Task Execute(SetPullRequestDescriptionCommandSettings settings)
    {
        var handler = new SetPullRequestDescriptionCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new GitHubClient(StdErrLogger, settings.GetGitHubClientSettings()),
            new StackConfig());

        await handler.Handle(new SetPullRequestDescriptionCommandInputs(settings.Stack));
    }
}

public record SetPullRequestDescriptionCommandInputs(string? Stack)
{
    public static SetPullRequestDescriptionCommandInputs Empty => new((string?)null);
}

public class SetPullRequestDescriptionCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<SetPullRequestDescriptionCommandInputs>
{
    public override async Task Handle(SetPullRequestDescriptionCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

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

        var status = StackHelpers.GetStackStatusNew(
            stack,
            currentBranch,
            logger,
            gitClient,
            gitHubClient,
            true);

        var pullRequestsInStack = new List<GitHubPullRequest>();

        foreach (var branch in status.Branches)
        {
            if (branch.PullRequest is not null)
            {
                pullRequestsInStack.Add(branch.PullRequest);
            }
        }

        if (pullRequestsInStack.Count == 0)
        {
            logger.Information($"No pull requests found for stack {stack.Name.Branch()}");
            return;
        }

        StackHelpers.UpdateStackPullRequestDescription(inputProvider, stackConfig, stacks, stack);
        StackHelpers.UpdateStackDescriptionInPullRequests(logger, gitHubClient, stack, pullRequestsInStack);
    }
}