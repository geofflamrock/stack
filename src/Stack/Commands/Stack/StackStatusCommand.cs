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

    [Description("Show status of all stacks.")]
    [CommandOption("--all")]
    public bool All { get; init; }
}

record BranchStatus(bool ExistsInRemote, int Ahead, int Behind);

internal class StackStatusCommand(IAnsiConsole console) : AsyncCommand<StackStatusCommandSettings>
{
    record StackStatus(Dictionary<string, BranchStatus> BranchStatuses, Dictionary<string, GitHubPullRequest> PullRequests);

    public override async Task<int> ExecuteAsync(CommandContext context, StackStatusCommandSettings settings)
    {
        await Task.CompletedTask;
        var stacks = StackConfig.Load();

        var remoteUri = GitOperations.GetRemoteUri(settings.GetGitOperationSettings());

        if (remoteUri is null)
        {
            console.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            console.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var currentBranch = GitOperations.GetCurrentBranch(settings.GetGitOperationSettings());

        var stacksToCheckStatusFor = new Dictionary<Config.Stack, StackStatus>();

        if (settings.All)
        {
            stacksForRemote.ForEach(stack => stacksToCheckStatusFor.Add(stack, new StackStatus([], [])));
        }
        else
        {
            var stackSelection = settings.Name ?? console.Prompt(Prompts.Stack(stacksForRemote, currentBranch));
            var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));
            stacksToCheckStatusFor.Add(stack, new StackStatus([], []));
        }

        console.Status()
            .Start("Checking status of remote branches...", ctx =>
            {
                foreach (var (stack, status) in stacksToCheckStatusFor
                )
                {
                    var allBranchesInStack = new List<string>([stack.SourceBranch]).Concat(stack.Branches).Distinct().ToArray();
                    var branchesThatExistInRemote = GitOperations.GetBranchesThatExistInRemote(allBranchesInStack, settings.GetGitOperationSettings());

                    GitOperations.FetchBranches(branchesThatExistInRemote, settings.GetGitOperationSettings());

                    void CheckRemoteBranch(string branch, string sourceBranch)
                    {
                        var (ahead, behind) = GitOperations.GetStatusOfRemoteBranch(branch, sourceBranch, settings.GetGitOperationSettings());
                        var branchStatus = new BranchStatus(true, ahead, behind);
                        status.BranchStatuses[branch] = branchStatus;
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
                            status.BranchStatuses[branch] = new BranchStatus(false, 0, 0);
                        }
                    }
                }
            });

        console.Status()
            .Start("Checking status of GitHub pull requests...", ctx =>
            {
                foreach (var (stack, status) in stacksToCheckStatusFor)
                {
                    try
                    {
                        foreach (var branch in stack.Branches)
                        {
                            var pr = GitHubOperations.GetPullRequest(branch, settings.GetGitHubOperationSettings());

                            if (pr is not null)
                            {
                                status.PullRequests[branch] = pr;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        console.MarkupLine($"[orange1]Error checking GitHub pull requests: {ex.Message}[/]");
                    }
                }
            });

        foreach (var (stack, status) in stacksToCheckStatusFor)
        {
            var stackRoot = new Tree($"[yellow]{stack.Name}:[/] [grey]{stack.SourceBranch}[/]");

            string BuildBranchName(string branch, string? parentBranch, bool isSourceBranchForStack)
            {
                var barnchStatus = status.BranchStatuses.GetValueOrDefault(branch);
                var branchNameBuilder = new StringBuilder();

                var color = barnchStatus?.ExistsInRemote == false ? "grey" : isSourceBranchForStack ? "grey" : branch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase) ? "blue" : null;
                Decoration? decoration = barnchStatus?.ExistsInRemote == false ? Decoration.Strikethrough : null;

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

                if (barnchStatus?.Ahead > 0 && barnchStatus?.Behind > 0)
                {
                    branchNameBuilder.Append($" [grey]({barnchStatus.Ahead} ahead, {barnchStatus.Behind} behind {parentBranch})[/]");
                }
                else if (barnchStatus?.Ahead > 0)
                {
                    branchNameBuilder.Append($" [grey]({barnchStatus.Ahead} ahead of {parentBranch})[/]");
                }
                else if (barnchStatus?.Behind > 0)
                {
                    branchNameBuilder.Append($" [grey]({barnchStatus.Behind} behind {parentBranch})[/]");
                }

                if (status.PullRequests.TryGetValue(branch, out var pr))
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

                if (status.BranchStatuses.TryGetValue(branch, out var branchStatus) && branchStatus.ExistsInRemote)
                {
                    parentBranch = branch;
                }
            }

            console.Write(stackRoot);
        }

        return 0;
    }
}
