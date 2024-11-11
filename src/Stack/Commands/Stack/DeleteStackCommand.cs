
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

internal class DeleteStackCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to delete.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

internal class DeleteStackCommand(
    IAnsiConsole console,
    IGitOperations gitOperations,
    IStackConfig stackConfig) : AsyncCommand<DeleteStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeleteStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri(settings.GetGitOperationSettings());
        var currentBranch = gitOperations.GetCurrentBranch(settings.GetGitOperationSettings());

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            console.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stackSelection = settings.Name ?? console.Prompt(Prompts.Stack(stacksForRemote, currentBranch));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        if (console.Prompt(new ConfirmationPrompt("Are you sure you want to delete this stack?")))
        {
            stacks.Remove(stack);
            stackConfig.Save(stacks);
            console.WriteLine($"Stack deleted");
        }

        return 0;
    }
}
