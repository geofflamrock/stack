using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class PullStackCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to show the status of.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

public class PullStackCommand : AsyncCommand<PullStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, PullStackCommandSettings settings)
    {
        var console = AnsiConsole.Console;

        var handler = new PullStackCommandHandler(
            new ConsoleInputProvider(console),
            new ConsoleOutputProvider(console),
            new GitOperations(console, settings.GetGitOperationSettings()),
            new StackConfig());

        await handler.Handle(new PullStackCommandInputs(settings.Name));

        return 0;
    }
}

public record PullStackCommandInputs(string? Name);
public class PullStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IStackConfig stackConfig)
{
    public async Task Handle(PullStackCommandInputs inputs)
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

        foreach (var branch in branchesThatExistInRemote)
        {
            outputProvider.Information($"Pulling changes for {branch.Branch()} from remote");
            gitOperations.ChangeBranch(branch);
            // await Task.Delay(100);
            gitOperations.PullBranch(branch);
        }

        gitOperations.ChangeBranch(currentBranch);

        // gitOperations.UpdateBranches(branchesThatExistInRemote);
    }
}
