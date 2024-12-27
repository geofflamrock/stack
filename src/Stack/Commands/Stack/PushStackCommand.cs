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
    [Description("The name of the stack to push changes from the remote for.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("Force the push of the stack.")]
    [CommandOption("-f|--force")]
    public bool Force { get; init; }

    [Description("Force the push of the stack with lease.")]
    [CommandOption("--force-with-lease")]
    public bool ForceWithLease { get; init; }

    [Description("The maximum number of branches to push changes for at once.")]
    [CommandOption("--max-batch-size")]
    [DefaultValue(5)]
    public int MaxBatchSize { get; init; } = 5;
}

public class PushStackCommand : AsyncCommand<PushStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, PushStackCommandSettings settings)
    {
        var console = AnsiConsole.Console;
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new PushStackCommandHandler(
            new ConsoleInputProvider(console),
            outputProvider,
            new GitOperations(outputProvider, settings.GetGitOperationSettings()),
            new StackConfig());

        await handler.Handle(new PushStackCommandInputs(settings.Name, settings.Force, settings.ForceWithLease, settings.MaxBatchSize));

        return 0;
    }
}

public record PushStackCommandInputs(string? Name, bool Force, bool ForceWithLease, int MaxBatchSize)
{
    public static PushStackCommandInputs Default => new(null, false, false, 5);
}

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

        var branchStatus = gitOperations.GetBranchStatuses([.. stack.Branches]);
        var branchesInStackWithRemote = branchStatus.Where(b => b.Value.RemoteBranchExists).Select(b => b.Value.BranchName).ToList();

        var branchGroupsToPush = branchesInStackWithRemote
            .Select((b, i) => new { Index = i, Value = b })
            .GroupBy(b => b.Index / inputs.MaxBatchSize)
            .Select(g => g.Select(b => b.Value).ToList())
            .ToList();

        foreach (var branches in branchGroupsToPush)
        {
            outputProvider.Information($"Pushing changes for {string.Join(", ", branches.Select(b => b.Branch()))} to remote");

            gitOperations.PushBranches([.. branches], inputs.Force, inputs.ForceWithLease);
        }
    }
}
