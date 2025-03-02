using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class PushStackCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to push changes from the remote for.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The maximum number of branches to push changes for at once.")]
    [CommandOption("--max-batch-size")]
    [DefaultValue(5)]
    public int MaxBatchSize { get; init; } = 5;

    [Description("Force push changes with lease.")]
    [CommandOption("--force-with-lease")]
    public bool ForceWithLease { get; init; }
}

public class PushStackCommand : CommandBase<PushStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, PushStackCommandSettings settings)
    {
        var handler = new PushStackCommandHandler(
            InputProvider,
            OutputProvider,
            new GitClient(OutputProvider, settings.GetGitClientSettings()),
            new StackConfig());

        await handler.Handle(new PushStackCommandInputs(
            settings.Stack,
            settings.MaxBatchSize,
            settings.ForceWithLease));

        return 0;
    }
}

public record PushStackCommandInputs(string? Stack, int MaxBatchSize, bool ForceWithLease)
{
    public static PushStackCommandInputs Default => new(null, 5, false);
}

public class PushStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitClient gitClient,
    IStackConfig stackConfig)
{
    public async Task Handle(PushStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            outputProvider.Information("No stacks found for current repository.");
            return;
        }

        var currentBranch = gitClient.GetCurrentBranch();

        var stack = inputProvider.SelectStack(outputProvider, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");

        StackHelpers.PushChanges(stack, inputs.MaxBatchSize, inputs.ForceWithLease, gitClient, outputProvider);
    }
}
