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

internal class NewBranchCommand(
    IAnsiConsole console,
    IGitOperations gitOperations,
    IStackConfig stackConfig) : AsyncCommand<NewBranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NewBranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var defaultBranch = gitOperations.GetDefaultBranch(settings.GetGitOperationSettings());
        var remoteUri = gitOperations.GetRemoteUri(settings.GetGitOperationSettings());
        var currentBranch = gitOperations.GetCurrentBranch(settings.GetGitOperationSettings());

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

        var branchName = settings.Name ?? console.Prompt(new TextPrompt<string>("Branch name:"));

        console.WriteLine($"Creating branch '{branchName}' from '{sourceBranch}' in stack '{stack.Name}'");

        gitOperations.CreateNewBranch(branchName, sourceBranch, settings.GetGitOperationSettings());
        gitOperations.PushNewBranch(branchName, settings.GetGitOperationSettings());

        stack.Branches.Add(branchName);

        stackConfig.Save(stacks);

        console.WriteLine($"Branch created");

        var switchToNewBranch = console.Prompt(new ConfirmationPrompt("Do you want to switch to the new branch?"));

        if (switchToNewBranch)
        {
            gitOperations.ChangeBranch(branchName, settings.GetGitOperationSettings());
        }

        return 0;
    }
}
