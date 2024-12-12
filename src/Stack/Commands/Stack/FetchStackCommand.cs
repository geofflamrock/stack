using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class FetchStackCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to fetch changes from the remote for.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

public class FetchStackCommand : AsyncCommand<FetchStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, FetchStackCommandSettings settings)
    {
        var console = AnsiConsole.Console;

        var handler = new FetchStackCommandHandler(
            new ConsoleInputProvider(console),
            new ConsoleOutputProvider(console),
            new GitOperations(console, settings.GetGitOperationSettings()),
            new StackConfig());

        await handler.Handle(new FetchStackCommandInputs(settings.Name));

        return 0;
    }
}

public record FetchStackCommandInputs(string? Name);
public class FetchStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IStackConfig stackConfig)
{
    public async Task Handle(FetchStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri();
        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            outputProvider.Information("No stacks found for current repository.");
            return;
        }

        var currentBranch = gitOperations.GetCurrentBranch();

        var stack = inputProvider.SelectStack(outputProvider, inputs.Name, stacksForRemote, currentBranch);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Name}' not found.");

        var branchesThatExistInRemote = gitOperations.GetBranchesThatExistInRemote([stack.SourceBranch, .. stack.Branches]);

        outputProvider.Information($"Fetching changes for {string.Join(", ", branchesThatExistInRemote.Select(b => b.Branch()))} from remote...");
        gitOperations.FetchBranches(branchesThatExistInRemote, true);
    }
}
