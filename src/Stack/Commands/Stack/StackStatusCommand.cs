using System.ComponentModel;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
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
        await Task.CompletedTask;
        var console = AnsiConsole.Console;
        var gitOperations = new GitOperations(console, settings.GetGitOperationSettings());

        var handler = new StackStatusCommandHandler(
            new StackStatusCommandInputProvider(new ConsoleInputProvider(console)),
            new ConsoleOutputProvider(console),
            gitOperations,
            new GitHubOperations(console, settings.GetGitHubOperationSettings()),
            new StackConfig());

        var response = await handler.Handle(new StackStatusCommandInputs(settings.Name, settings.All));

        var currentBranch = gitOperations.GetCurrentBranch();

        foreach (var (stack, status) in response.Statuses)
        {
            var stackRoot = new Tree($"[yellow]{stack.Name}:[/] [grey]{stack.SourceBranch}[/]");

            string BuildBranchName(string branch, string? parentBranch, bool isSourceBranchForStack)
            {
                var branchDetail = status.Branches.GetValueOrDefault(branch);
                var branchNameBuilder = new StringBuilder();

                var color = branchDetail?.Status.ExistsInRemote == false ? "grey" : isSourceBranchForStack ? "grey" : branch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase) ? "blue" : null;
                Decoration? decoration = branchDetail?.Status.ExistsInRemote == false || branchDetail?.Status.ExistsLocally == false ? Decoration.Strikethrough : null;

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

                if (branchDetail?.Status.Ahead > 0 && branchDetail?.Status.Behind > 0)
                {
                    branchNameBuilder.Append($" [grey]({branchDetail.Status.Ahead} ahead, {branchDetail.Status.Behind} behind {parentBranch})[/]");
                }
                else if (branchDetail?.Status.Ahead > 0)
                {
                    branchNameBuilder.Append($" [grey]({branchDetail.Status.Ahead} ahead of {parentBranch})[/]");
                }
                else if (branchDetail?.Status.Behind > 0)
                {
                    branchNameBuilder.Append($" [grey]({branchDetail.Status.Behind} behind {parentBranch})[/]");
                }

                if (branchDetail?.PullRequest is not null)
                {
                    branchNameBuilder.Append($" {branchDetail.PullRequest.GetPullRequestDisplay()}");
                }

                return branchNameBuilder.ToString();
            }

            string parentBranch = stack.SourceBranch;

            foreach (var branch in stack.Branches)
            {
                stackRoot.AddNode(BuildBranchName(branch, parentBranch, false));

                if (status.Branches.TryGetValue(branch, out var branchDetail) && branchDetail.Status.ExistsInRemote)
                {
                    parentBranch = branch;
                }
            }

            console.Write(stackRoot);
        }

        if (response.Statuses.Count == 1)
        {
            var (stack, status) = response.Statuses.First();

            bool BranchCouldBeCleanedUp(BranchDetail branchDetail)
            {
                return branchDetail.Status.ExistsLocally &&
                        (!branchDetail.Status.ExistsInRemote ||
                        branchDetail.PullRequest is not null && branchDetail.PullRequest.State != GitHubPullRequestStates.Open);
            }

            if (status.Branches.Values.All(branch => BranchCouldBeCleanedUp(branch)))
            {
                console.WriteLine();
                console.MarkupLine("All branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open. This stack might be able to be deleted.");
                console.WriteLine();
                console.MarkupLine($"Run [aqua]stack delete --name \"{stack.Name}\"[/] to delete the stack if it's no longer needed.");
            }
            else if (status.Branches.Values.Any(branch => BranchCouldBeCleanedUp(branch)))
            {
                console.WriteLine();
                console.MarkupLine("Some branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open.");
                console.WriteLine();
                console.MarkupLine($"Run [aqua]stack cleanup --name \"{stack.Name}\"[/] to clean up local branches.");
            }
            else if (status.Branches.Values.All(branch => !branch.Status.ExistsLocally))
            {
                console.WriteLine();
                console.MarkupLine("No branches exist locally. This stack might be able to be deleted.");
                console.WriteLine();
                console.MarkupLine($"Run [aqua]stack delete --name \"{stack.Name}\"[/] to delete the stack.");
            }

            if (status.Branches.Values.Any(branch => branch.Status.ExistsInRemote && branch.Status.ExistsLocally && branch.Status.Behind > 0))
            {
                console.WriteLine();
                console.MarkupLine("There are changes in source branches that have not been applied to the stack.");
                console.WriteLine();
                console.MarkupLine($"Run [aqua]stack update --name \"{stack.Name}\"[/] to update the stack.");
            }
        }

        return 0;
    }
}

public record StackStatusCommandInputs(string? Name, bool All);
public record StackStatusCommandResponse(Dictionary<Config.Stack, StackStatus> Statuses);

public interface IStackStatusCommandInputProvider
{
    string SelectStack(List<Config.Stack> stacks, string currentBranch);
}

public class StackStatusCommandInputProvider(IInputProvider inputProvider) : IStackStatusCommandInputProvider
{
    public const string SelectStackPrompt = "Select stack:";

    public string SelectStack(List<Config.Stack> stacks, string currentBranch)
    {
        return inputProvider.Select(
            SelectStackPrompt,
            stacks
                .OrderByCurrentStackThenByName(currentBranch)
                .Select(s => s.Name)
                .ToArray());
    }
}



public class StackStatusCommandHandler(
    IStackStatusCommandInputProvider inputProvider,
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

        var stacksToCheckStatusFor = new Dictionary<Config.Stack, StackStatus>();

        if (inputs.All)
        {
            stacksForRemote
                .OrderByCurrentStackThenByName(currentBranch)
                .ToList()
                .ForEach(stack => stacksToCheckStatusFor.Add(stack, new StackStatus([])));
        }
        else
        {
            var stackSelection = inputs.Name ?? inputProvider.SelectStack(stacksForRemote, currentBranch);
            var stack = stacksForRemote.FirstOrDefault(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));
            if (stack is null)
            {
                throw new InvalidOperationException($"Stack '{stackSelection}' not found.");
            }

            stacksToCheckStatusFor.Add(stack, new StackStatus([]));
        }

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

        return new StackStatusCommandResponse(stacksToCheckStatusFor);
    }
}