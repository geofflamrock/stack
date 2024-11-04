using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

internal class AddBranchCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to add.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

internal class AddBranchCommand : AsyncCommand<AddBranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AddBranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var defaultBranch = GitOperations.GetDefaultBranch(settings.GetGitOperationSettings());
        var remoteUri = GitOperations.GetRemoteUri(settings.GetGitOperationSettings());
        var currentBranch = GitOperations.GetCurrentBranch(settings.GetGitOperationSettings());
        var branches = GitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate(settings.GetGitOperationSettings());

        var stacks = StackConfig.Load();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stackSelection = settings.Stack ?? AnsiConsole.Prompt(Prompts.Stack(stacksForRemote));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        var sourceBranch = stack.Branches.LastOrDefault() ?? stack.SourceBranch;

        var branchName = settings.Name ?? AnsiConsole.Prompt(Prompts.Branch(branches));

        AnsiConsole.WriteLine($"Adding branch '{branchName}' to stack '{stack.Name}'");

        stack.Branches.Add(branchName);

        StackConfig.Save(stacks);

        AnsiConsole.WriteLine($"Branch added");
        return 0;
    }
}
