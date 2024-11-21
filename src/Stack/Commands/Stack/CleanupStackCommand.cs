
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Commands.Helpers;

namespace Stack.Commands;

public class CleanupStackCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to cleanup.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("Cleanup the stack without prompting.")]
    [CommandOption("-f|--force")]
    public bool Force { get; init; }
}

public class CleanupStackCommand : AsyncCommand<CleanupStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CleanupStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var console = AnsiConsole.Console;
        var handler = new CleanupStackCommandHandler(
            new ConsoleInputProvider(console),
            new ConsoleOutputProvider(console),
            new GitOperations(console, settings.GetGitOperationSettings()),
            new StackConfig());

        await handler.Handle(new CleanupStackCommandInputs(settings.Name, settings.Force));

        return 0;
    }
}

public record CleanupStackCommandInputs(string? Name, bool Force)
{
    public static CleanupStackCommandInputs Empty => new(null, false);
}

public record CleanupStackCommandResponse(string? CleanedUpStackName);

public class CleanupStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IStackConfig stackConfig)
{
    public async Task Handle(CleanupStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri();
        var currentBranch = gitOperations.GetCurrentBranch();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        var stackNames = stacks.OrderByCurrentStackThenByName(currentBranch).Select(s => s.Name).ToArray();
        var stackSelection = inputs.Name ?? inputProvider.Select(Questions.SelectStack, stackNames);
        var stack = stacksForRemote.FirstOrDefault(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Name}' not found.");
        }

        var branchesInTheStackThatExistLocally = gitOperations.GetBranchesThatExistLocally([.. stack.Branches]);
        var branchesInTheStackThatExistInTheRemote = gitOperations.GetBranchesThatExistInRemote([.. stack.Branches]);

        var branchesToCleanUp = GetBranchesNeedingCleanup(gitOperations, stack);

        if (branchesToCleanUp.Length == 0)
        {
            outputProvider.Information("No branches to clean up");
            return;
        }

        if (!inputs.Force)
        {
            OutputBranchesNeedingCleanup(outputProvider, branchesToCleanUp);
        }

        if (inputs.Force || inputProvider.Confirm(Questions.ConfirmDeleteBranches))
        {
            CleanupBranches(gitOperations, outputProvider, branchesToCleanUp);

            outputProvider.Information($"Stack {stack.Name.Stack()} cleaned up");
        }
    }

    public static string[] GetBranchesNeedingCleanup(IGitOperations gitOperations, Config.Stack stack)
    {
        var branchesInTheStackThatExistLocally = gitOperations.GetBranchesThatExistLocally([.. stack.Branches]);
        var branchesInTheStackThatExistInTheRemote = gitOperations.GetBranchesThatExistInRemote([.. stack.Branches]);

        return branchesInTheStackThatExistLocally.Except(branchesInTheStackThatExistInTheRemote).ToArray();
    }

    public static void OutputBranchesNeedingCleanup(IOutputProvider outputProvider, string[] branches)
    {
        outputProvider.Information("The following branches exist locally but not on the remote:");

        foreach (var branch in branches)
        {
            outputProvider.Information($"  {branch.Branch()}");
        }
    }

    public static void CleanupBranches(IGitOperations gitOperations, IOutputProvider outputProvider, string[] branches)
    {
        foreach (var branch in branches)
        {
            outputProvider.Information($"Deleting local branch {branch.Branch()}");
            gitOperations.DeleteLocalBranch(branch);
        }
    }
}
