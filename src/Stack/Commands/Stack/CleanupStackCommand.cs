
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

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
            new CleanupStackCommandInputProvider(new ConsoleInputProvider(console)),
            new ConsoleOutputProvider(console),
            new GitOperations(console, settings.GetGitOperationSettings()),
            new StackConfig());

        await handler.Handle(new CleanupStackCommandInputs(settings.Name, settings.Force));

        return 0;
    }
}

public interface ICleanupStackCommandInputProvider
{
    string SelectStack(List<Config.Stack> stacks, string currentBranch);
    bool ConfirmCleanup();
}

public class CleanupStackCommandInputProvider(IInputProvider inputProvider) : ICleanupStackCommandInputProvider
{
    const string SelectStackPrompt = "Select stack:";
    const string CleanupStackPrompt = "Do you want to continue?";

    public string SelectStack(List<Config.Stack> stacks, string currentBranch)
    {
        return inputProvider.Select(SelectStackPrompt, stacks.OrderByCurrentStackThenByName(currentBranch).Select(s => s.Name).ToArray());
    }

    public bool ConfirmCleanup()
    {
        return inputProvider.Confirm(CleanupStackPrompt);
    }
}

public record CleanupStackCommandInputs(string? Name, bool Force)
{
    public static CleanupStackCommandInputs Empty => new(null, false);
}

public record CleanupStackCommandResponse(string? CleanedUpStackName);

public class CleanupStackCommandHandler(
    ICleanupStackCommandInputProvider inputProvider,
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

        var stackSelection = inputs.Name ?? inputProvider.SelectStack(stacksForRemote, currentBranch);
        var stack = stacksForRemote.FirstOrDefault(s => s.Name.Equals(stackSelection, StringComparison.OrdinalIgnoreCase));

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Name}' not found.");
        }

        var branchesInTheStackThatExistLocally = gitOperations.GetBranchesThatExistLocally([.. stack.Branches]);
        var branchesInTheStackThatExistInTheRemote = gitOperations.GetBranchesThatExistInRemote([.. stack.Branches]);

        var branchesToCleanUp = branchesInTheStackThatExistLocally.Except(branchesInTheStackThatExistInTheRemote).ToList();

        if (branchesToCleanUp.Count == 0)
        {
            outputProvider.Information("No branches to clean up");
            return;
        }

        if (!inputs.Force)
        {
            outputProvider.Information($"The following branches from stack {stack.Name.Stack()} will be deleted:");

            foreach (var branch in branchesToCleanUp)
            {
                outputProvider.Information($"  {branch.Branch()}");
            }
        }

        if (inputs.Force || inputProvider.ConfirmCleanup())
        {
            foreach (var branch in stack.Branches)
            {
                if (!branchesInTheStackThatExistInTheRemote.Contains(branch) &&
                    branchesInTheStackThatExistLocally.Contains(branch))
                {
                    outputProvider.Information($"Deleting local branch {branch.Branch()}");
                    gitOperations.DeleteLocalBranch(branch);
                }
            }

            outputProvider.Information($"Stack {stack.Name.Stack()} cleaned up");
        }
    }
}
