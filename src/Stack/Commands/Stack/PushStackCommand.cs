using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class PushStackCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to show the status of.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("Force the push of the stack.")]
    [CommandOption("-f|--force")]
    public bool Force { get; init; }

    [Description("Force the push of the stack with lease.")]
    [CommandOption("--force-with-lease")]
    public bool ForceWithLease { get; init; }
}

public class PushStackCommand : AsyncCommand<PushStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, PushStackCommandSettings settings)
    {
        var console = AnsiConsole.Console;

        var handler = new PushStackCommandHandler(
            new ConsoleInputProvider(console),
            new ConsoleOutputProvider(console),
            new GitOperations(console, settings.GetGitOperationSettings()),
            new StackConfig());

        await handler.Handle(new PushStackCommandInputs(settings.Name, settings.Force, settings.ForceWithLease));

        return 0;
    }
}

public record PushStackCommandInputs(string? Name, bool Force, bool ForceWithLease);
public class PushStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IStackConfig stackConfig)
{
    public async Task Handle(PushStackCommandInputs inputs)
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

        var branchesThatExistInRemote = gitOperations.GetBranchesThatExistInRemote([.. stack.Branches]);

        outputProvider.Information($"Pushing changes for {string.Join(", ", branchesThatExistInRemote.Select(b => b.Branch()))} to remote...");
        gitOperations.PushBranches(branchesThatExistInRemote, inputs.Force, inputs.ForceWithLease);
    }
}
