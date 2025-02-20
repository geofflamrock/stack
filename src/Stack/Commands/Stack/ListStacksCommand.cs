using System.ComponentModel;
using System.Text.Json;
using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class ListStacksCommandSettings : CommandSettingsBase
{
    [Description("Output the list of stacks in JSON format.")]
    [CommandOption("--json")]
    public bool Json { get; init; }
}

public class ListStacksCommand : AsyncCommand<ListStacksCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ListStacksCommandSettings settings)
    {
        await Task.CompletedTask;
        var console = AnsiConsole.Console;
        var outputProvider = new ConsoleOutputProvider(console);
        var gitClient = new GitClient(outputProvider, settings.GetGitClientSettings());
        var stackConfig = new StackConfig();

        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

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

        if (settings.Json)
        {
            console.WriteLine(JsonSerializer.Serialize(stacksForRemote));
            return 0;
        }

        foreach (var stack in stacksForRemote)
        {
            console.MarkupLine($"[yellow]{stack.Name}[/] [grey]({stack.SourceBranch})[/] {"branch".ToQuantity(stack.Branches.Count)}");
        }

        return 0;
    }
}
