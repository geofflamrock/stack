using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

internal class CreatePullRequestsCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to update.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

internal class CreatePullRequestsCommand(
    IAnsiConsole console,
    IGitOperations gitOperations,
    IGitHubOperations gitHubOperations,
    IStackConfig stackConfig) : AsyncCommand<CreatePullRequestsCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CreatePullRequestsCommandSettings settings)
    {
        await Task.CompletedTask;

        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri(settings.GetGitOperationSettings());

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            console.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var currentBranch = gitOperations.GetCurrentBranch(settings.GetGitOperationSettings());
        var stackSelection = settings.Name ?? AnsiConsole.Prompt(Prompts.Stack(stacksForRemote, currentBranch));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        console.MarkupLine($"Stack: {stack.Name}");

        if (console.Prompt(new ConfirmationPrompt("Are you sure you want to create pull requests for branches in this stack?")))
        {
            var sourceBranch = stack.SourceBranch;

            foreach (var branch in stack.Branches)
            {
                var existingPullRequest = gitHubOperations.GetPullRequest(branch, settings.GetGitHubOperationSettings());

                if (existingPullRequest is not null && existingPullRequest.State != GitHubPullRequestStates.Closed)
                {
                    console.MarkupLine($"Pull request [{existingPullRequest.GetPullRequestColor()} link={existingPullRequest.Url}]#{existingPullRequest.Number}: {existingPullRequest.Title}[/] already exists for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]. Skipping...");
                }
                else
                {
                    if (gitOperations.DoesRemoteBranchExist(branch, settings.GetGitOperationSettings()))
                    {
                        var prTitle = console.Prompt(new TextPrompt<string>($"Pull request title for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]:"));
                        console.MarkupLine($"Creating pull request for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]");
                        var pullRequest = gitHubOperations.CreatePullRequest(branch, sourceBranch, prTitle, "test", settings.GetGitHubOperationSettings());

                        if (pullRequest is not null)
                        {
                            console.MarkupLine($"Pull request [{pullRequest.GetPullRequestColor()} link={pullRequest.Url}]#{pullRequest.Number}: {pullRequest.Title}[/] created for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]");
                        }

                        sourceBranch = branch;
                    }
                    else
                    {
                        // Remote branch no longer exists, skip over
                        console.MarkupLine($"[red]Branch '{branch}' no longer exists on the remote repository. Skipping...[/]");
                    }
                }
            }
        }

        return 0;
    }
}
