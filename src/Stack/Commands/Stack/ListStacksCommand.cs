using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

internal class ListStacksCommandSettings : CommandSettingsBase;

internal class ListStacksCommand : AsyncCommand<ListStacksCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ListStacksCommandSettings settings)
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

        foreach (var stack in stacksForRemote)
        {
            AnsiConsole.MarkupLine($"[yellow]{stack.Name}[/] [grey]({stack.SourceBranch})[/] {"branch".ToQuantity(stack.Branches.Count)}");
        }

        return 0;
    }
}
