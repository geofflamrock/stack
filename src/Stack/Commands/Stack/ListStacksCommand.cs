using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;

namespace Stack.Commands;

public class ListStacksCommandSettings : CommandSettingsBase;

public class ListStacksCommand() : AsyncCommand<ListStacksCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ListStacksCommandSettings settings)
    {
        await Task.CompletedTask;
        var console = AnsiConsole.Console;
        var gitOperations = new GitOperations(console, settings.GetGitOperationSettings());
        var stackConfig = new StackConfig();

        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri();

        if (remoteUri is null)
        {
            console.WriteLine("No stacks found for current repository.");
            return 0;
        }

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            console.WriteLine("No stacks found for current repository.");
            return 0;
        }

        foreach (var stack in stacksForRemote)
        {
            console.MarkupLine($"[yellow]{stack.Name}[/] [grey]({stack.SourceBranch})[/] {"branch".ToQuantity(stack.Branches.Count)}");
        }

        return 0;
    }
}
