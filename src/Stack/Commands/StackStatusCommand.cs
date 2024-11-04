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

        var stackSelection = settings.Name ?? AnsiConsole.Prompt(new SelectionPrompt<string>().Title("Select stack:").PageSize(10).AddChoices(stacksForRemote.Select(s => s.Name).ToArray()));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        var remoteStatusForBranchesInStacks = new Dictionary<string, BranchStatus>();


        AnsiConsole.Status()
            .Start("Checking status of remote branches...", ctx =>
            {
                var allBranchesInStack = new List<string>([stack.SourceBranch]).Concat(stack.Branches).Distinct().ToArray();

                var branchesThatExistInRemote = GitOperations.GetBranchesThatExistInRemote(allBranchesInStack, settings.GetGitOperationSettings());
                GitOperations.FetchBranches(branchesThatExistInRemote, settings.GetGitOperationSettings());


                var branchesThatHaveBeenMergedForStack = GitOperations.GetBranchesThatHaveBeenMerged([.. stack.Branches], stack.SourceBranch, settings.GetGitOperationSettings());

                // AnsiConsole.MarkupLine($"[grey]Branches that have been merged into '{stack.SourceBranch}': {string.Join(", ", branchesThatHaveBeenMergedForStack)}[/]");

                void CheckRemoteBranch(string branch, string sourceBranch)
                {
                    // GitOperations.FetchBranch(branch);
                    // GitOperations.FetchBranch(sourceBranch);
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


        var currentBranch = GitOperations.GetCurrentBranch(settings.GetGitOperationSettings());

        var stackRoot = new Tree($"[yellow]{stack.Name}[/]");
        var markup = new Markup($"[grey]({stack.RemoteUri})[/]");
        var sourceBranchNode = stackRoot.AddNode(BuildBranchName(stack.SourceBranch, null, true));

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

            if (status?.ExistsInRemote == false)
            {
                branchNameBuilder.Append($" [{Decoration.Strikethrough} grey](deleted in remote)[/]");
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

            return branchNameBuilder.ToString();
        }

        string parentBranch = stack.SourceBranch;

        foreach (var branch in stack.Branches)
        {
            sourceBranchNode.AddNode(BuildBranchName(branch, parentBranch, false));

            if (remoteStatusForBranchesInStacks.TryGetValue(branch, out var status) && status.ExistsInRemote)
            {
                parentBranch = branch;
            }
        }

        AnsiConsole.Write(stackRoot);

        return 0;
    }
}
