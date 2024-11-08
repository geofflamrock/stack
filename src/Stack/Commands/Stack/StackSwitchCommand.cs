using System.ComponentModel;
using System.Text;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

internal class StackSwitchCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to switch to.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to switch to.")]
    [CommandOption("-b|--branch")]
    public string? Branch { get; init; }
}

internal class StackSwitchCommand : AsyncCommand<StackStatusCommandSettings>
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

        var branchSelectionPrompt = new SelectionPrompt<string>()
            .Title("Select branch")
            .PageSize(10);

        var currentBranch = GitOperations.GetCurrentBranch(settings.GetGitOperationSettings());

        foreach (var stack in stacksForRemote.OrderByCurrentStackThenByName(currentBranch))
        {
            var allBranchesInStack = new List<string>([stack.SourceBranch, .. stack.Branches]).ToArray();
            var branchesThatExistLocally = GitOperations.GetBranchesThatExistLocally(allBranchesInStack, settings.GetGitOperationSettings());
            branchSelectionPrompt.AddChoiceGroup(stack.Name, branchesThatExistLocally);
        }

        var selectedBranch = AnsiConsole.Prompt(branchSelectionPrompt);

        GitOperations.ChangeBranch(selectedBranch, settings.GetGitOperationSettings());

        return 0;
    }
}
