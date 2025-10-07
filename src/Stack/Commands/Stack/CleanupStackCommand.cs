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
        CleanupStackCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<CleanupStackCommand> logger)
        : base("cleanup", "Clean up branches in a stack that are no longer needed.", executionContext, inputProvider, outputProvider, logger)
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
    IGitClientFactory gitClientFactory,
    CliExecutionContext executionContext,
    IGitHubClient gitHubClient,
    IStackRepository repository)
    : CommandHandlerBase<CleanupStackCommandInputs>
{
    public override async Task Handle(CleanupStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = repository.GetStacks();

        var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
        }

        var branchesToCleanUp = StackHelpers.GetBranchesNeedingCleanup(stack, logger, gitClient, gitHubClient);

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
