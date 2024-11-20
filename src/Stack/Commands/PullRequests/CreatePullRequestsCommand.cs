using System.ComponentModel;
using System.Diagnostics;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

public class CreatePullRequestsCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to create pull requests for.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

public class CreatePullRequestsCommand() : AsyncCommand<CreatePullRequestsCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CreatePullRequestsCommandSettings settings)
    {
        var console = AnsiConsole.Console;
        var gitOperations = new GitOperations(console, settings.GetGitOperationSettings());
        var gitHubOperations = new GitHubOperations(console);
        var stackConfig = new StackConfig();

        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            console.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var currentBranch = gitOperations.GetCurrentBranch();
        var stackSelection = settings.Name ?? console.Prompt(Prompts.Stack(stacksForRemote, currentBranch));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        await new StackStatusCommand()
            .ExecuteAsync(context, new StackStatusCommandSettings
            {
                Name = stack.Name,
                WorkingDirectory = settings.WorkingDirectory,
                Verbose = settings.Verbose
            });

        console.WriteLine();

        if (console.Prompt(new ConfirmationPrompt("Are you sure you want to create pull requests for branches in this stack?")))
        {
            var sourceBranch = stack.SourceBranch;
            var pullRequestsInStack = new List<GitHubPullRequest>();

            foreach (var branch in stack.Branches)
            {
                var existingPullRequest = gitHubOperations.GetPullRequest(branch, settings.GetGitHubOperationSettings());

                if (existingPullRequest is not null && existingPullRequest.State != GitHubPullRequestStates.Closed)
                {
                    console.MarkupLine($"Pull request {existingPullRequest.GetPullRequestDisplay()} already exists for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]. Skipping...");
                    pullRequestsInStack.Add(existingPullRequest);
                }

                if (gitOperations.DoesRemoteBranchExist(branch))
                {
                    if (existingPullRequest is null || existingPullRequest.State == GitHubPullRequestStates.Closed)
                    {
                        var prTitle = console.Prompt(new TextPrompt<string>($"Pull request title for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]:"));
                        console.MarkupLine($"Creating pull request for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]");
                        var pullRequest = gitHubOperations.CreatePullRequest(branch, sourceBranch, prTitle, "", settings.GetGitHubOperationSettings());

                        if (pullRequest is not null)
                        {
                            console.MarkupLine($"Pull request {pullRequest.GetPullRequestDisplay()} created for branch [blue]{branch}[/] to [blue]{sourceBranch}[/]");
                            pullRequestsInStack.Add(pullRequest);
                        }
                    }

                    sourceBranch = branch;
                }
            }

            if (pullRequestsInStack.Count > 1)
            {
                var defaultStackDescription = stack.PullRequestDescription ?? $"This PR is part of a stack **{stack.Name}**:";
                var stackDescription = console.Prompt(new TextPrompt<string>("Stack description for PR:").DefaultValue(defaultStackDescription));

                if (stackDescription != stack.PullRequestDescription)
                {
                    stack.SetPullRequestDescription(stackDescription);
                    stackConfig.Save(stacks);
                }

                // Edit each PR and add to the top of the description
                // the details of each PR in the stack
                var stackMarkerStart = "<!-- stack-pr-list -->";
                var stackMarkerEnd = "<!-- /stack-pr-list -->";
                var prList = pullRequestsInStack
                    .Select(pr => $"- {pr.Url}")
                    .ToList();
                var prListMarkdown = string.Join("\n", prList);
                var prBodyMarkdown = $"{stackMarkerStart}\n{stackDescription}\n\n{prListMarkdown}\n{stackMarkerEnd}";

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

                    if (prBody.Length > 0 && prListStart == 0)
                    {
                        // Add some newlines so that the PR list is separated from the rest of the PR body
                        prBody = prBody.Insert(prListStart, prBodyMarkdown + "\n\n");
                    }
                    else
                    {
                        prBody = prBody.Insert(prListStart, prBodyMarkdown);
                    }

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
