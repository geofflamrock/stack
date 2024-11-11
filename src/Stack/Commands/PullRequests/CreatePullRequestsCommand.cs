using System.ComponentModel;
using System.Diagnostics;
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
        var stackSelection = settings.Name ?? console.Prompt(Prompts.Stack(stacksForRemote, currentBranch));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        console.MarkupLine($"Stack: {stack.Name}");

        if (console.Prompt(new ConfirmationPrompt("Are you sure you want to create pull requests for branches in this stack?")))
        {
            var sourceBranch = stack.SourceBranch;
            var pullRequestsInStack = new List<GitHubPullRequest>();

            foreach (var branch in stack.Branches)
            {
                var existingPullRequest = gitHubOperations.GetPullRequest(branch, settings.GetGitHubOperationSettings());

                if (existingPullRequest is not null && existingPullRequest.State != GitHubPullRequestStates.Closed)
                {
                    console.MarkupLine($"Pull request [{existingPullRequest.GetPullRequestColor()} link={existingPullRequest.Url}]#{existingPullRequest.Number}: {existingPullRequest.Title}[/] already exists for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]. Skipping...");
                    pullRequestsInStack.Add(existingPullRequest);
                }
                else
                {
                    if (gitOperations.DoesRemoteBranchExist(branch, settings.GetGitOperationSettings()))
                    {
                        var prTitle = console.Prompt(new TextPrompt<string>($"Pull request title for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]:"));
                        console.MarkupLine($"Creating pull request for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]");
                        var pullRequest = gitHubOperations.CreatePullRequest(branch, sourceBranch, prTitle, "", settings.GetGitHubOperationSettings());

                        if (pullRequest is not null)
                        {
                            console.MarkupLine($"Pull request [{pullRequest.GetPullRequestColor()} link={pullRequest.Url}]#{pullRequest.Number}: {pullRequest.Title}[/] created for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]");
                            pullRequestsInStack.Add(pullRequest);
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

            if (pullRequestsInStack.Count > 1)
            {
                // Edit each PR and add to the top of the description
                // the details of each PR in the stack
                var stackMarkerStart = "<!-- stack-pr-list -->";
                var stackMarkerEnd = "<!-- /stack-pr-list -->";
                var prList = pullRequestsInStack
                    .Select(pr => $"- {pr.Url}")
                    .ToList();
                var prListMarkdown = string.Join("\n", prList);
                var prListHeader = $"This PR is part of a stack **{stack.Name}**:";
                var prBodyMarkdown = $"{stackMarkerStart}\n{prListHeader}\n\n{prListMarkdown}\n{stackMarkerEnd}";

                foreach (var pullRequest in pullRequestsInStack)
                {
                    // Find the existing part of the PR body that has the PR list
                    // and replace it with the updated PR list
                    var prBody = pullRequest.Body;

                    var prListStart = prBody.IndexOf(stackMarkerStart, StringComparison.OrdinalIgnoreCase);
                    var prListEnd = prBody.IndexOf(stackMarkerEnd, StringComparison.OrdinalIgnoreCase);

                    if (prListStart >= 0 && prListEnd >= 0)
                    {
                        prBody = prBody.Remove(prListStart, prListEnd - prListStart + stackMarkerEnd.Length);
                    }

                    if (prListStart == -1)
                    {
                        prListStart = 0;
                    }

                    prBody = prBody.Insert(prListStart, prBodyMarkdown);

                    gitHubOperations.EditPullRequest(pullRequest.Number, prBody, settings.GetGitHubOperationSettings());
                }
            }
            else
            {
                console.MarkupLine("Only one pull request in stack, not adding PR list to description.");
            }

            if (console.Prompt(new ConfirmationPrompt("Open the pull requests in the browser?")))
            {
                foreach (var pullRequest in pullRequestsInStack)
                {
                    Process.Start(new ProcessStartInfo(pullRequest.Url.ToString())
                    {
                        UseShellExecute = true
                    });
                }
            }
        }

        return 0;
    }
}
