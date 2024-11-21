using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

public class AddBranchCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to add.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

public class AddBranchCommand : AsyncCommand<AddBranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AddBranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var console = AnsiConsole.Console;
        var gitOperations = new GitOperations(console, settings.GetGitOperationSettings());
        var stackConfig = new StackConfig();

        var defaultBranch = gitOperations.GetDefaultBranch();
        var remoteUri = gitOperations.GetRemoteUri();
        var currentBranch = gitOperations.GetCurrentBranch();
        var branches = gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var stacks = stackConfig.Load();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            console.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stackSelection = settings.Stack ?? console.Prompt(Prompts.Stack(stacksForRemote, currentBranch));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        var sourceBranch = stack.Branches.LastOrDefault() ?? stack.SourceBranch;

        var branchName = settings.Name ?? console.Prompt(Prompts.Branch(branches));

        console.WriteLine($"Adding branch '{branchName}' to stack '{stack.Name}'");

        stack.Branches.Add(branchName);

        stackConfig.Save(stacks);

        console.WriteLine($"Branch added");
        return 0;
    }
}
