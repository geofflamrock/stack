
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

internal class DeleteStackCommand : AsyncCommand<DeleteStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, DeleteStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var stacks = StackConfig.Load();

        var remoteUri = GitOperations.GetRemoteUri(settings.GetGitOperationSettings());

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            AnsiConsole.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stackSelection = settings.Name ?? AnsiConsole.Prompt(Prompts.Stack(stacksForRemote));
        var stack = stacksForRemote.First(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        if (AnsiConsole.Prompt(new ConfirmationPrompt("Are you sure you want to delete this stack?")))
        {
            stacks.Remove(stack);
            StackConfig.Save(stacks);
            AnsiConsole.WriteLine($"Stack deleted");
        }

        return 0;
    }
}
