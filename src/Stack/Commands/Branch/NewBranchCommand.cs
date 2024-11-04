using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

internal class NewBranchCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to create.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

internal class NewBranchCommand : AsyncCommand<NewBranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NewBranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var defaultBranch = GitOperations.GetDefaultBranch(settings.GetGitOperationSettings());
        var remoteUri = GitOperations.GetRemoteUri(settings.GetGitOperationSettings());
        var currentBranch = GitOperations.GetCurrentBranch(settings.GetGitOperationSettings());

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

        var branchName = settings.Name ?? AnsiConsole.Prompt(new TextPrompt<string>("Branch name:"));

        AnsiConsole.WriteLine($"Creating branch '{branchName}' from '{sourceBranch}' in stack '{stack.Name}'");

        GitOperations.CreateNewBranch(branchName, sourceBranch, settings.GetGitOperationSettings());
        GitOperations.PushNewBranch(branchName, settings.GetGitOperationSettings());

        stack.Branches.Add(branchName);

        StackConfig.Save(stacks);

        AnsiConsole.WriteLine($"Branch created");
        return 0;
    }
}
