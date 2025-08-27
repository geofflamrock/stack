using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Commands.Helpers;
using System.CommandLine;
using Microsoft.Extensions.Logging;

namespace Stack.Commands;

public class CleanupStackCommand : Command
{
    private readonly CleanupStackCommandHandler handler;

    public CleanupStackCommand(
        ILogger<CleanupStackCommand> logger,
        IAnsiConsoleWriter console,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        CleanupStackCommandHandler handler)
        : base("cleanup", "Clean up branches in a stack that are no longer needed.", logger, console, inputProvider, executionContext)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(CommonOptions.Confirm);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new CleanupStackCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(CommonOptions.Confirm)),
            cancellationToken);
    }
}

public record CleanupStackCommandInputs(string? Stack, bool Confirm)
{
    public static CleanupStackCommandInputs Empty => new(null, false);
}

public class CleanupStackCommandHandler(
    IInputProvider inputProvider,
    ILogger<CleanupStackCommandHandler> logger,
    IAnsiConsoleWriter console,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<CleanupStackCommandInputs>
{
    public override async Task Handle(CleanupStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
        }

        var branchesToCleanUp = StackHelpers.GetBranchesNeedingCleanup(stack, logger, console, gitClient, gitHubClient);

        if (branchesToCleanUp.Length == 0)
        {
            logger.LogInformation("No branches to clean up");
            return;
        }

        StackHelpers.OutputBranchesNeedingCleanup(logger, branchesToCleanUp);

        if (inputs.Confirm || await inputProvider.Confirm(Questions.ConfirmDeleteBranches, cancellationToken))
        {
            StackHelpers.CleanupBranches(gitClient, logger, branchesToCleanUp);
            logger.LogInformation($"Stack {stack.Name.Stack()} cleaned up");
        }
    }
}
