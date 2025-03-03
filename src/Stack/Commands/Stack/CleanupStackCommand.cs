
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
}

public class CleanupStackCommand : CommandBase<CleanupStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CleanupStackCommandSettings settings)
    {
        var handler = new CleanupStackCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new GitHubClient(StdErrLogger, settings.GetGitHubClientSettings()),
            new StackConfig());

        await handler.Handle(new CleanupStackCommandInputs(settings.Stack));

        return 0;
    }
}

public record CleanupStackCommandInputs(string? Stack)
{
    public static CleanupStackCommandInputs Empty => new((string?)null);
}

public record CleanupStackCommandResponse(string? CleanedUpStackName);

public class CleanupStackCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
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

        var stack = inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
        }

        var branchesToCleanUp = StackHelpers.GetBranchesNeedingCleanup(stack, logger, gitClient, gitHubClient);

        if (branchesToCleanUp.Length == 0)
        {
            logger.Information("No branches to clean up");
            return;
        }

        StackHelpers.OutputBranchesNeedingCleanup(logger, branchesToCleanUp);

        if (inputProvider.Confirm(Questions.ConfirmDeleteBranches))
        {
            StackHelpers.CleanupBranches(gitClient, logger, branchesToCleanUp);

            logger.Information($"Stack {stack.Name.Stack()} cleaned up");
        }
    }
}
