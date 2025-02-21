
using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Commands.Helpers;

namespace Stack.Commands;

public class CleanupStackCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to cleanup.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("Confirm all prompts")]
    [CommandOption("--yes")]
    public bool Confirm { get; init; }
}

public class CleanupStackCommand : AsyncCommand<CleanupStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CleanupStackCommandSettings settings)
    {
        await Task.CompletedTask;

        var console = AnsiConsole.Console;
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new CleanupStackCommandHandler(
            new ConsoleInputProvider(console),
            outputProvider,
            new GitClient(outputProvider, settings.GetGitClientSettings()),
            new GitHubClient(outputProvider, settings.GetGitHubClientSettings()),
            new StackConfig());

        await handler.Handle(new CleanupStackCommandInputs(settings.Stack, settings.Confirm));

        return 0;
    }
}

public record CleanupStackCommandInputs(string? Stack, bool Confirm = false)
{
    public static CleanupStackCommandInputs Empty => new(null, false);
}

public record CleanupStackCommandResponse(string? CleanedUpStackName);

public class CleanupStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
{
    public async Task Handle(CleanupStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        var stack = inputProvider.SelectStack(outputProvider, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
        }

        var branchesToCleanUp = StackHelpers.GetBranchesNeedingCleanup(stack, outputProvider, gitClient, gitHubClient);

        if (branchesToCleanUp.Length == 0)
        {
            outputProvider.Information("No branches to clean up");
            return;
        }

        StackHelpers.OutputBranchesNeedingCleanup(outputProvider, branchesToCleanUp);

        if (inputs.Confirm || inputProvider.Confirm(Questions.ConfirmDeleteBranches))
        {
            StackHelpers.CleanupBranches(gitClient, outputProvider, branchesToCleanUp);

            outputProvider.Information($"Stack {stack.Name.Stack()} cleaned up");
        }
    }
}
