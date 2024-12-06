using System.ComponentModel;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class StackStatusCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to show the status of.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("Show status of all stacks.")]
    [CommandOption("--all")]
    public bool All { get; init; }
}

public class BranchDetail
{
    public BranchStatus Status { get; set; } = new(false, false, 0, 0);
    public GitHubPullRequest? PullRequest { get; set; }
}
public record BranchStatus(bool ExistsLocally, bool ExistsInRemote, int Ahead, int Behind);
public record StackStatus(Dictionary<string, BranchDetail> Branches);

public class StackStatusCommand : AsyncCommand<StackStatusCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StackStatusCommandSettings settings)
    {
        var console = AnsiConsole.Console;

        var handler = new StackStatusCommandHandler(
            new ConsoleInputProvider(console),
            new ConsoleOutputProvider(console),
            new GitOperations(console, settings.GetGitOperationSettings()),
            new GitHubOperations(console, settings.GetGitHubOperationSettings()),
            new StackConfig());

        await handler.Handle(new StackStatusCommandInputs(settings.Name, settings.All));

        return 0;
    }
}

public record StackStatusCommandInputs(string? Name, bool All);
public record StackStatusCommandResponse(Dictionary<Config.Stack, StackStatus> Statuses);

public class StackStatusCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IGitHubOperations gitHubOperations,
    IStackConfig stackConfig)
{
    public async Task<StackStatusCommandResponse> Handle(StackStatusCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri();
        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();
        var currentBranch = gitOperations.GetCurrentBranch();

        var stacksToCheckStatusFor = new List<Config.Stack>();

        if (inputs.All)
        {
            stacksToCheckStatusFor.AddRange(stacks);
        }
        else
        {
            var stack = inputProvider.SelectStack(outputProvider, inputs.Name, stacksForRemote, currentBranch);

            if (stack is null)
            {
                throw new InvalidOperationException($"Stack '{inputs.Name}' not found.");
            }

            stacksToCheckStatusFor.Add(stack);
        }

        var stackStatusResults = StackStatusHelpers.CheckStackStatus(
            stacksToCheckStatusFor,
            currentBranch,
            outputProvider,
            gitOperations,
            gitHubOperations);

        StackStatusHelpers.OutputStackStatus(stackStatusResults, gitOperations, outputProvider);

        if (stacksToCheckStatusFor.Count == 1)
        {
            var (stack, status) = stackStatusResults.First();
            StackStatusHelpers.OutputBranchAndStackCleanup(stack, status, outputProvider);
        }

        return new StackStatusCommandResponse(stackStatusResults);
    }
}

public static class StackStatusHelpers
{
    public static Dictionary<Config.Stack, StackStatus> CheckStackStatus(
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

        outputProvider.Status("Checking status of remote branches...", () =>
        {
            foreach (var (stack, status) in stacksToCheckStatusFor)
            {
                var allBranchesInStack = new List<string>([stack.SourceBranch]).Concat(stack.Branches).Distinct().ToArray();
                var branchesThatExistInRemote = gitOperations.GetBranchesThatExistInRemote(allBranchesInStack);
                var branchesThatExistLocally = gitOperations.GetBranchesThatExistLocally(allBranchesInStack);

                gitOperations.FetchBranches(branchesThatExistInRemote);

                void CheckRemoteBranch(string branch, string sourceBranch)
                {
                    var branchExistsLocally = branchesThatExistLocally.Contains(branch);
                    var (ahead, behind) = gitOperations.GetStatusOfRemoteBranch(branch, sourceBranch);
                    var branchStatus = new BranchStatus(branchExistsLocally, true, ahead, behind);
                    status.Branches[branch].Status = branchStatus;
                }

                var parentBranch = stack.SourceBranch;

                foreach (var branch in stack.Branches)
                {
                    status.Branches.Add(branch, new BranchDetail());

                    if (branchesThatExistInRemote.Contains(branch))
                    {
                        CheckRemoteBranch(branch, parentBranch);
                        parentBranch = branch;
                    }
                    else
                    {
                        status.Branches[branch].Status = new BranchStatus(branchesThatExistLocally.Contains(branch), false, 0, 0);
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

    public static void OutputStackStatus(
        Dictionary<Config.Stack, StackStatus> stackStatuses,
        IGitOperations gitOperations,
        IOutputProvider outputProvider)
    {
        foreach (var (stack, status) in stackStatuses)
        {
            var header = $"{stack.Name.Stack()}: {stack.SourceBranch.Muted()}";
            var items = new List<string>();

            string parentBranch = stack.SourceBranch;

            foreach (var branch in stack.Branches)
            {
                if (status.Branches.TryGetValue(branch, out var branchDetail))
                {
                    items.Add(GetBranchAndPullRequestStatusOutput(branch, parentBranch, branchDetail, gitOperations));

                    if (branchDetail.Status.ExistsInRemote &&
                        (branchDetail.PullRequest is null || branchDetail.PullRequest.State != GitHubPullRequestStates.Merged))
                    {
                        parentBranch = branch;
                    }
                }
            }
            outputProvider.Tree(header, [.. items]);
        }
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
        var branchIsMerged =
            branchDetail.Status.ExistsInRemote == false ||
            branchDetail.Status.ExistsLocally == false ||
            branchDetail.PullRequest is not null && branchDetail.PullRequest.State == GitHubPullRequestStates.Merged;

        var color = branchIsMerged ? "grey" : branch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase) ? "blue" : null;
        Decoration? decoration = branchIsMerged ? Decoration.Strikethrough : null;

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

        if (!branchIsMerged)
        {
            if (branchDetail.Status.Ahead > 0 && branchDetail.Status.Behind > 0)
            {
                branchNameBuilder.Append($" [grey]({branchDetail.Status.Ahead} ahead, {branchDetail.Status.Behind} behind {parentBranch})[/]");
            }
            else if (branchDetail.Status.Ahead > 0)
            {
                branchNameBuilder.Append($" [grey]({branchDetail.Status.Ahead} ahead of {parentBranch})[/]");
            }
            else if (branchDetail.Status.Behind > 0)
            {
                branchNameBuilder.Append($" [grey]({branchDetail.Status.Behind} behind {parentBranch})[/]");
            }
        }

        return branchNameBuilder.ToString();
    }

    public static void OutputBranchAndStackCleanup(
        Config.Stack stack,
        StackStatus status,
        IOutputProvider outputProvider)
    {
        bool BranchCouldBeCleanedUp(BranchDetail branchDetail)
        {
            return branchDetail.Status.ExistsLocally &&
                    (!branchDetail.Status.ExistsInRemote ||
                    branchDetail.PullRequest is not null && branchDetail.PullRequest.State != GitHubPullRequestStates.Open);
        }

        if (status.Branches.Values.All(branch => BranchCouldBeCleanedUp(branch)))
        {
            outputProvider.NewLine();
            outputProvider.Information("All branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open. This stack might be able to be deleted.");
            outputProvider.NewLine();
            outputProvider.Information($"Run {$"stack delete --name \"{stack.Name}\"".Example()} to delete the stack if it's no longer needed.");
        }
        else if (status.Branches.Values.Any(branch => BranchCouldBeCleanedUp(branch)))
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

        if (status.Branches.Values.Any(branch => branch.Status.ExistsInRemote && branch.Status.ExistsLocally && branch.Status.Behind > 0))
        {
            outputProvider.NewLine();
            outputProvider.Information("There are changes in source branches that have not been applied to the stack.");
            outputProvider.NewLine();
            outputProvider.Information($"Run {$"stack update --name \"{stack.Name}\"".Example()} to update the stack.");
        }
    }
}
