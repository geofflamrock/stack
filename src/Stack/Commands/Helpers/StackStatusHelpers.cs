using System.Text;
using Spectre.Console;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands.Helpers;

public class BranchDetail
{
    public BranchStatus Status { get; set; } = new(false, false, 0, 0, 0, 0);
    public GitHubPullRequest? PullRequest { get; set; }

    public bool IsActive => Status.ExistsLocally && Status.ExistsInRemote && (PullRequest is null || PullRequest.State != GitHubPullRequestStates.Merged);
    public bool CouldBeCleanedUp => Status.ExistsLocally && (!Status.ExistsInRemote || PullRequest is not null && PullRequest.State == GitHubPullRequestStates.Merged);
    public bool HasPullRequest => PullRequest is not null && PullRequest.State != GitHubPullRequestStates.Closed;
}
public record BranchStatus(bool ExistsLocally, bool ExistsInRemote, int AheadOfParent, int BehindParent, int AheadOfRemote, int BehindRemote);
public record StackStatus(Dictionary<string, BranchDetail> Branches)
{
    public string[] GetActiveBranches() => Branches.Where(b => b.Value.IsActive).Select(b => b.Key).ToArray();
}

public static class StackStatusHelpers
{
    public static Dictionary<Config.Stack, StackStatus> GetStackStatus(
        List<Config.Stack> stacks,
        string currentBranch,
        IOutputProvider outputProvider,
        IGitOperations gitOperations,
        IGitHubOperations gitHubOperations)
    {
        var stacksToCheckStatusFor = new Dictionary<Config.Stack, StackStatus>();

        stacks
            .OrderByCurrentStackThenByName(currentBranch)
            .ToList()
            .ForEach(stack => stacksToCheckStatusFor.Add(stack, new StackStatus([])));

        var allBranchesInStacks = stacks.SelectMany(s => new List<string>([s.SourceBranch]).Concat(s.Branches)).Distinct().ToArray();
        var branchesThatExistInRemote = gitOperations.GetBranchesThatExistInRemote(allBranchesInStacks);
        var branchesThatExistLocally = gitOperations.GetBranchesThatExistLocally(allBranchesInStacks);

        outputProvider.Status("Fetching branches...", () =>
        {
            gitOperations.FetchBranches(branchesThatExistInRemote);
        });

        outputProvider.Status("Checking status of branches...", () =>
        {
            foreach (var (stack, status) in stacksToCheckStatusFor)
            {
                void CheckBranchStatus(string branch, string sourceBranch)
                {
                    var branchExistsLocally = branchesThatExistLocally.Contains(branch);
                    var (ahead, behind) = gitOperations.CompareBranches(branch, sourceBranch);
                    var (aheadRemote, behindRemote) = gitOperations.GetComparisonToRemoteTrackingBranch(branch);
                    var branchStatus = new BranchStatus(branchExistsLocally, true, ahead, behind, aheadRemote, behindRemote);
                    status.Branches[branch].Status = branchStatus;
                }

                var parentBranch = stack.SourceBranch;

                foreach (var branch in stack.Branches)
                {
                    status.Branches.Add(branch, new BranchDetail());

                    if (branchesThatExistInRemote.Contains(branch))
                    {
                        CheckBranchStatus(branch, parentBranch);
                        parentBranch = branch;
                    }
                    else
                    {
                        status.Branches[branch].Status = new BranchStatus(branchesThatExistLocally.Contains(branch), false, 0, 0, 0, 0);
                    }
                }
            }
        });

        outputProvider.Status("Checking status of GitHub pull requests...", () =>
        {
            foreach (var (stack, status) in stacksToCheckStatusFor)
            {
                try
                {
                    foreach (var branch in stack.Branches)
                    {
                        var pr = gitHubOperations.GetPullRequest(branch);

                        if (pr is not null)
                        {
                            status.Branches[branch].PullRequest = pr;
                        }
                    }
                }
                catch (Exception ex)
                {
                    outputProvider.Warning($"Error checking GitHub pull requests: {ex.Message}");
                }
            }
        });

        return stacksToCheckStatusFor;
    }

    public static StackStatus GetStackStatus(
        Config.Stack stack,
        string currentBranch,
        IOutputProvider outputProvider,
        IGitOperations gitOperations,
        IGitHubOperations gitHubOperations)
    {
        var statues = GetStackStatus([stack], currentBranch, outputProvider, gitOperations, gitHubOperations);

        return statues[stack];
    }

    public static void OutputStackStatus(
        Dictionary<Config.Stack, StackStatus> stackStatuses,
        IGitOperations gitOperations,
        IOutputProvider outputProvider)
    {
        foreach (var (stack, status) in stackStatuses)
        {
            OutputStackStatus(stack, status, gitOperations, outputProvider);
        }
    }

    public static void OutputStackStatus(
        Config.Stack stack,
        StackStatus status,
        IGitOperations gitOperations,
        IOutputProvider outputProvider)
    {
        var (aheadRemote, behindRemote) = gitOperations.GetComparisonToRemoteTrackingBranch(stack.SourceBranch);
        var header = $"{stack.Name.Stack()}: {stack.SourceBranch.Muted()}";
        if (aheadRemote > 0 || behindRemote > 0)
        {
            header += $" {behindRemote}{Emoji.Known.DownArrow}{aheadRemote}{Emoji.Known.UpArrow}".Muted();
        }

        var items = new List<string>();

        string parentBranch = stack.SourceBranch;

        foreach (var branch in stack.Branches)
        {
            if (status.Branches.TryGetValue(branch, out var branchDetail))
            {
                items.Add(GetBranchAndPullRequestStatusOutput(branch, parentBranch, branchDetail, gitOperations));

                if (branchDetail.IsActive)
                {
                    parentBranch = branch;
                }
            }
        }

        outputProvider.Tree(header, [.. items]);
    }

    public static string GetBranchAndPullRequestStatusOutput(
        string branch,
        string parentBranch,
        BranchDetail branchDetail,
        IGitOperations gitOperations)
    {
        var branchNameBuilder = new StringBuilder();
        branchNameBuilder.Append(GetBranchStatusOutput(branch, parentBranch, branchDetail, gitOperations));

        if (branchDetail.PullRequest is not null)
        {
            branchNameBuilder.Append($" {branchDetail.PullRequest.GetPullRequestDisplay()}");
        }

        return branchNameBuilder.ToString();
    }

    public static string GetBranchStatusOutput(
        string branch,
        string parentBranch,
        BranchDetail branchDetail,
        IGitOperations gitOperations)
    {
        var branchNameBuilder = new StringBuilder();
        var currentBranch = gitOperations.GetCurrentBranch();

        var color = !branchDetail.IsActive ? "grey" : branch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase) ? "blue" : null;
        Decoration? decoration = !branchDetail.IsActive ? Decoration.Strikethrough : null;

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

        if (branchDetail.IsActive)
        {
            if (branchDetail.Status.AheadOfRemote > 0 || branchDetail.Status.BehindRemote > 0)
            {
                branchNameBuilder.Append($" {branchDetail.Status.BehindRemote}{Emoji.Known.DownArrow}{branchDetail.Status.AheadOfRemote}{Emoji.Known.UpArrow}".Muted());
            }

            if (branchDetail.Status.AheadOfParent > 0 && branchDetail.Status.BehindParent > 0)
            {
                branchNameBuilder.Append($" [grey]({branchDetail.Status.AheadOfParent} ahead, {branchDetail.Status.BehindParent} behind {parentBranch})[/]");
            }
            else if (branchDetail.Status.AheadOfParent > 0)
            {
                branchNameBuilder.Append($" [grey]({branchDetail.Status.AheadOfParent} ahead of {parentBranch})[/]");
            }
            else if (branchDetail.Status.BehindParent > 0)
            {
                branchNameBuilder.Append($" [grey]({branchDetail.Status.BehindParent} behind {parentBranch})[/]");
            }
        }

        return branchNameBuilder.ToString();
    }

    public static void OutputBranchAndStackCleanup(
        Config.Stack stack,
        StackStatus status,
        IOutputProvider outputProvider)
    {
        if (status.Branches.Values.All(branch => branch.CouldBeCleanedUp))
        {
            outputProvider.NewLine();
            outputProvider.Information("All branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open. This stack might be able to be deleted.");
            outputProvider.NewLine();
            outputProvider.Information($"Run {$"stack delete --name \"{stack.Name}\"".Example()} to delete the stack if it's no longer needed.");
        }
        else if (status.Branches.Values.Any(branch => branch.CouldBeCleanedUp))
        {
            outputProvider.NewLine();
            outputProvider.Information("Some branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open.");
            outputProvider.NewLine();
            outputProvider.Information($"Run {$"stack cleanup --name \"{stack.Name}\"".Example()} to clean up local branches.");
        }
        else if (status.Branches.Values.All(branch => !branch.Status.ExistsLocally))
        {
            outputProvider.NewLine();
            outputProvider.Information("No branches exist locally. This stack might be able to be deleted.");
            outputProvider.NewLine();
            outputProvider.Information($"Run {$"stack delete --name \"{stack.Name}\"".Example()} to delete the stack.");
        }

        if (status.Branches.Values.Any(branch => branch.Status.ExistsInRemote && branch.Status.ExistsLocally && branch.Status.BehindParent > 0))
        {
            outputProvider.NewLine();
            outputProvider.Information("There are changes in source branches that have not been applied to the stack.");
            outputProvider.NewLine();
            outputProvider.Information($"Run {$"stack update --name \"{stack.Name}\"".Example()} to update the stack.");
        }
    }
}
