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

public record BranchStatus(bool ExistsInRemote, int Ahead, int Behind);
public record StackStatus(Dictionary<string, BranchStatus> BranchStatuses, Dictionary<string, GitHubPullRequest> PullRequests);

public class StackStatusCommand : AsyncCommand<StackStatusCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StackStatusCommandSettings settings)
    {
        await Task.CompletedTask;
        var console = AnsiConsole.Console;
        var gitOperations = new GitOperations(console);

        var handler = new StackStatusCommandHandler(
            new StackStatusCommandInputProvider(new ConsoleInputProvider(console)),
            new ConsoleOutputProvider(console),
            gitOperations,
            new GitHubOperations(console),
            new StackConfig());

        var response = await handler.Handle(
            new StackStatusCommandInputs(settings.Name, settings.All),
            settings.GetGitOperationSettings(),
            settings.GetGitHubOperationSettings());

        var currentBranch = gitOperations.GetCurrentBranch(settings.GetGitOperationSettings());

        foreach (var (stack, status) in response.Statuses)
        {
            var stackRoot = new Tree($"[yellow]{stack.Name}:[/] [grey]{stack.SourceBranch}[/]");

            string BuildBranchName(string branch, string? parentBranch, bool isSourceBranchForStack)
            {
                var branchStatus = status.BranchStatuses.GetValueOrDefault(branch);
                var branchNameBuilder = new StringBuilder();

                var color = branchStatus?.ExistsInRemote == false ? "grey" : isSourceBranchForStack ? "grey" : branch.Equals(currentBranch, StringComparison.OrdinalIgnoreCase) ? "blue" : null;
                Decoration? decoration = branchStatus?.ExistsInRemote == false ? Decoration.Strikethrough : null;

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

                if (branchStatus?.Ahead > 0 && branchStatus?.Behind > 0)
                {
                    branchNameBuilder.Append($" [grey]({branchStatus.Ahead} ahead, {branchStatus.Behind} behind {parentBranch})[/]");
                }
                else if (branchStatus?.Ahead > 0)
                {
                    branchNameBuilder.Append($" [grey]({branchStatus.Ahead} ahead of {parentBranch})[/]");
                }
                else if (branchStatus?.Behind > 0)
                {
                    branchNameBuilder.Append($" [grey]({branchStatus.Behind} behind {parentBranch})[/]");
                }

                if (status.PullRequests.TryGetValue(branch, out var pr))
                {
                    branchNameBuilder.Append($" {pr.GetPullRequestDisplay()}");
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
    public async Task<StackStatusCommandResponse> Handle(
        StackStatusCommandInputs inputs,
        GitOperationSettings gitOperationSettings,
        GitHubOperationSettings gitHubOperationSettings)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri(gitOperationSettings);
        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();
        var currentBranch = gitOperations.GetCurrentBranch(gitOperationSettings);

        var stacksToCheckStatusFor = new Dictionary<Config.Stack, StackStatus>();

        if (inputs.All)
        {
            stacksForRemote
                .OrderByCurrentStackThenByName(currentBranch)
                .ToList()
                .ForEach(stack => stacksToCheckStatusFor.Add(stack, new StackStatus([], [])));
        }
        else
        {
            var stackSelection = inputs.Name ?? inputProvider.SelectStack(stacksForRemote, currentBranch);
            var stack = stacksForRemote.FirstOrDefault(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));
            if (stack is null)
            {
                throw new InvalidOperationException($"Stack '{stackSelection}' not found.");
            }

            stacksToCheckStatusFor.Add(stack, new StackStatus([], []));
        }

        outputProvider.Status("Checking status of remote branches...", () =>
        {
            foreach (var (stack, status) in stacksToCheckStatusFor)
            {
                var allBranchesInStack = new List<string>([stack.SourceBranch]).Concat(stack.Branches).Distinct().ToArray();
                var branchesThatExistInRemote = gitOperations.GetBranchesThatExistInRemote(allBranchesInStack, gitOperationSettings);

                gitOperations.FetchBranches(branchesThatExistInRemote, gitOperationSettings);

                void CheckRemoteBranch(string branch, string sourceBranch)
                {
                    var (ahead, behind) = gitOperations.GetStatusOfRemoteBranch(branch, sourceBranch, gitOperationSettings);
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

        outputProvider.Status("Checking status of GitHub pull requests...", () =>
        {
            foreach (var (stack, status) in stacksToCheckStatusFor)
            {
                try
                {
                    foreach (var branch in stack.Branches)
                    {
                        var pr = gitHubOperations.GetPullRequest(branch, gitHubOperationSettings);

                        if (pr is not null)
                        {
                            status.PullRequests[branch] = pr;
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