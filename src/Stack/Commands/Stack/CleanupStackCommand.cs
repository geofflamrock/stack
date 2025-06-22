using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Commands.Helpers;
using System.CommandLine;

namespace Stack.Commands;

public class CleanupStackCommand : Command
{
    public CleanupStackCommand() : base("cleanup", "Clean up branches in a stack that are no longer needed.")
    {
        Add(CommonOptions.Stack);
        Add(CommonOptions.Confirm);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var handler = new CleanupStackCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, new GitClientSettings(Verbose, WorkingDirectory)),
            new GitHubClient(StdErrLogger, new GitHubClientSettings(Verbose, WorkingDirectory)),
            new FileStackConfig());

        await handler.Handle(new CleanupStackCommandInputs(
            parseResult.GetValue(CommonOptions.Stack),
            parseResult.GetValue(CommonOptions.Confirm)));
    }
}

public record CleanupStackCommandInputs(string? Stack, bool Confirm)
{
    public static CleanupStackCommandInputs Empty => new(null, false);
}

public class CleanupStackCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<CleanupStackCommandInputs>
{
    public override async Task Handle(CleanupStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

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

        if (inputs.Confirm || inputProvider.Confirm(Questions.ConfirmDeleteBranches))
        {
            StackHelpers.CleanupBranches(gitClient, logger, branchesToCleanUp);
            logger.Information($"Stack {stack.Name.Stack()} cleaned up");
        }
    }
}
