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

internal class CreatePullRequestsCommand : AsyncCommand<CreatePullRequestsCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CreatePullRequestsCommandSettings settings)
    {
        await Task.CompletedTask;

        var stacks = StackConfig.Load();

        var remoteUri = GitOperations.GetRemoteUri(settings.GetGitOperationSettings());

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var currentBranch = GitOperations.GetCurrentBranch(settings.GetGitOperationSettings());
        var stackSelection = settings.Name ?? AnsiConsole.Prompt(Prompts.Stack(stacksForRemote, currentBranch));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        AnsiConsole.MarkupLine($"Stack: {stack.Name}");

        if (AnsiConsole.Prompt(new ConfirmationPrompt("Are you sure you want to create pull requests for branches in this stack?")))
        {
            var sourceBranch = stack.SourceBranch;

            foreach (var branch in stack.Branches)
            {
                if (GitOperations.DoesRemoteBranchExist(branch, settings.GetGitOperationSettings()))
                {
                    var existingPullRequest = GitHubOperations.GetPullRequest(branch, settings.GetGitHubOperationSettings());

                    if (existingPullRequest is not null)
                    {
                        AnsiConsole.MarkupLine($"Pull request already exists for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]. Skipping...");
                        continue;
                    }

                    var prTitle = AnsiConsole.Prompt(new TextPrompt<string>($"Pull request title for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]:"));

                    AnsiConsole.MarkupLine($"Creating pull request for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]");
                    sourceBranch = branch;
                }
                else
                {
                    // Remote branch no longer exists, skip over
                    AnsiConsole.MarkupLine($"[red]Branch '{branch}' no longer exists on the remote repository. Skipping...[/]");
                }
            }
        }

        return 0;
    }
}
