using System.ComponentModel;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

internal class StackStatusCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to show the status of.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

record BranchStatus(bool ExistsInRemote, int Ahead, int Behind);

internal class StackStatusCommand : AsyncCommand<StackStatusCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StackStatusCommandSettings settings)
    {
        await Task.CompletedTask;
        var stacks = StackConfig.Load();

        var remoteUri = GitOperations.GetRemoteUri(settings.GetGitOperationSettings());

        if (remoteUri is null)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var currentBranch = GitOperations.GetCurrentBranch(settings.GetGitOperationSettings());

        var stackSelection = settings.Name ?? AnsiConsole.Prompt(Prompts.Stack(stacksForRemote, currentBranch));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        var allBranchesInStack = new List<string>([stack.SourceBranch]).Concat(stack.Branches).Distinct().ToArray();
        var branchesThatExistInRemote = GitOperations.GetBranchesThatExistInRemote(allBranchesInStack, settings.GetGitOperationSettings());

        var remoteStatusForBranchesInStacks = new Dictionary<string, BranchStatus>();
        var prForBranchesInStacks = new Dictionary<string, GitHubPullRequest>();

        AnsiConsole.Status()
            .Start("Checking status of remote branches...", ctx =>
            {
                GitOperations.FetchBranches(branchesThatExistInRemote, settings.GetGitOperationSettings());

                void CheckRemoteBranch(string branch, string sourceBranch)
                {
                    var (ahead, behind) = GitOperations.GetStatusOfRemoteBranch(branch, sourceBranch, settings.GetGitOperationSettings());
                    var branchStatus = new BranchStatus(true, ahead, behind);
                    remoteStatusForBranchesInStacks[branch] = branchStatus;
                }

                var parentBranch = stack.SourceBranch;

                foreach (var branch in stack.Branches)
                {
                    if (branchesThatExistInRemote.Contains(branch))
                    {
                        CheckRemoteBranch(branch, parentBranch);
                        parentBranch = branch;
                    }
                    else
                    {
                        remoteStatusForBranchesInStacks[branch] = new BranchStatus(false, 0, 0);
                    }
                }
            });

        AnsiConsole.Status()
            .Start("Checking status of GitHub pull requests...", ctx =>
            {
                try
                {
                    foreach (var branch in stack.Branches)
                    {
                        var pr = GitHubOperations.GetPullRequest(branch, settings.GetGitHubOperationSettings());

                        if (pr is not null)
                        {
                            prForBranchesInStacks[branch] = pr;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[orange1]Error checking GitHub pull requests: {ex.Message}[/]");
                }
            });

        var stackRoot = new Tree($"[yellow]{stack.Name}:[/] [grey]{stack.SourceBranch}[/]");

        string BuildBranchName(string branch, string? parentBranch, bool isSourceBranchForStack)
        {
            var status = remoteStatusForBranchesInStacks.GetValueOrDefault(branch);
            var branchNameBuilder = new StringBuilder();

            var color = status?.ExistsInRemote == false ? "grey" : isSourceBranchForStack ? "grey" : branch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase) ? "blue" : null;
            Decoration? decoration = status?.ExistsInRemote == false ? Decoration.Strikethrough : null;

            if (color is not null && decoration is not null)
            {
                branchNameBuilder.Append($"[{decoration} {color}]{branch}[/]");
            }
            else if (color is not null)
            {
                branchNameBuilder.Append($"[{color}]{branch}[/]");
            }
            else if (decoration is not null)
            {
                branchNameBuilder.Append($"[{decoration}]{branch}[/]");
            }
            else
            {
                branchNameBuilder.Append(branch);
            }

            if (status?.Ahead > 0 && status?.Behind > 0)
            {
                branchNameBuilder.Append($" [grey]({status.Ahead} ahead, {status.Behind} behind {parentBranch})[/]");
            }
            else if (status?.Ahead > 0)
            {
                branchNameBuilder.Append($" [grey]({status.Ahead} ahead of {parentBranch})[/]");
            }
            else if (status?.Behind > 0)
            {
                branchNameBuilder.Append($" [grey]({status.Behind} behind {parentBranch})[/]");
            }

            if (prForBranchesInStacks.TryGetValue(branch, out var pr))
            {
                var prStatusColor = Color.Green;
                if (pr.State == GitHubPullRequestStates.Merged)
                {
                    prStatusColor = Color.Purple;
                }
                else if (pr.State == GitHubPullRequestStates.Closed)
                {
                    prStatusColor = Color.Red;
                }
                branchNameBuilder.Append($" [{prStatusColor} link={pr.Url}]#{pr.Number}: {pr.Title}[/]");
            }

            return branchNameBuilder.ToString();
        }

        string parentBranch = stack.SourceBranch;

        foreach (var branch in stack.Branches)
        {
            stackRoot.AddNode(BuildBranchName(branch, parentBranch, false));

            if (remoteStatusForBranchesInStacks.TryGetValue(branch, out var status) && status.ExistsInRemote)
            {
                parentBranch = branch;
            }
        }

        AnsiConsole.Write(stackRoot);

        return 0;
    }
}
