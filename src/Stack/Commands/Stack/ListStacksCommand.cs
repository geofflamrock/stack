using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class ListStacksCommandSettings : CommandSettingsBase;

public class ListStacksCommand : CommandBase<ListStacksCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ListStacksCommandSettings settings)
    {
        await Task.CompletedTask;
        var gitClient = new GitClient(OutputProvider, settings.GetGitClientSettings());
        var stackConfig = new StackConfig();

        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        if (remoteUri is null)
        {
            OutputProvider.Information("No stacks found for current repository.");
            return 0;
        }

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            OutputProvider.Information("No stacks found for current repository.");
            return 0;
        }

        foreach (var stack in stacksForRemote)
        {
            OutputProvider.Information($"[yellow]{stack.Name}[/] [grey]({stack.SourceBranch})[/] {"branch".ToQuantity(stack.Branches.Count)}");
        }

        return 0;
    }
}
