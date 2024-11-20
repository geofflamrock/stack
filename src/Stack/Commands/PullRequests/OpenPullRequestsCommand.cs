using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

public class OpenPullRequestsCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to open PRs for.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

public class OpenPullRequestsCommand() : AsyncCommand<OpenPullRequestsCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, OpenPullRequestsCommandSettings settings)
    {
        await Task.CompletedTask;

        var console = AnsiConsole.Console;
        var gitOperations = new GitOperations(console);
        var gitHubOperations = new GitHubOperations(console);
        var stackConfig = new StackConfig();

        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri(settings.GetGitOperationSettings());

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            console.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var currentBranch = gitOperations.GetCurrentBranch(settings.GetGitOperationSettings());
        var stackSelection = settings.Name ?? console.Prompt(Prompts.Stack(stacksForRemote, currentBranch));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        var pullRequestsInStack = new List<GitHubPullRequest>();

        foreach (var branch in stack.Branches)
        {
            var existingPullRequest = gitHubOperations.GetPullRequest(branch, settings.GetGitHubOperationSettings());

            if (existingPullRequest is not null && existingPullRequest.State != GitHubPullRequestStates.Closed)
            {
                pullRequestsInStack.Add(existingPullRequest);
            }
        }

        if (pullRequestsInStack.Count == 0)
        {
            console.MarkupLine($"No pull requests found for stack [yellow]{stack.Name}[/]");
            return 0;
        }

        foreach (var pullRequest in pullRequestsInStack)
        {
            Process.Start(new ProcessStartInfo(pullRequest.Url.ToString())
            {
                UseShellExecute = true
            });
        }

        return 0;
    }
}
