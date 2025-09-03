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
        IDisplayProvider displayProvider,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        CleanupStackCommandHandler handler)
        : base("cleanup", "Clean up branches in a stack that are no longer needed.", logger, displayProvider, inputProvider, executionContext)
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
    IDisplayProvider displayProvider,
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

        var branchesToCleanUp = StackHelpers.GetBranchesNeedingCleanup(stack, logger, displayProvider, gitClient, gitHubClient);

        if (branchesToCleanUp.Length == 0)
        {
            logger.NoBranchesToCleanUp();
            return;
        }

        StackHelpers.OutputBranchesNeedingCleanup(logger, branchesToCleanUp);

        if (inputs.Confirm || await inputProvider.Confirm(Questions.ConfirmDeleteBranches, cancellationToken))
        {
            StackHelpers.CleanupBranches(gitClient, logger, branchesToCleanUp);
            logger.StackCleanedUp(stack.Name);
        }
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Information, Message = "No branches to clean up")]
    public static partial void NoBranchesToCleanUp(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stack \"{Stack}\" cleaned up")]
    public static partial void StackCleanedUp(this ILogger logger, string stack);
}
