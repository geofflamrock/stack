using System.CommandLine;
using Microsoft.Extensions.Logging;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class PushStackCommand : Command
{
    static readonly Option<bool> ForceWithLease = new("--force-with-lease")
    {
        Description = "Force push changes with lease."
    };

    private readonly PushStackCommandHandler handler;

    public PushStackCommand(
        PushStackCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<PushStackCommand> logger)
        : base("push", "Push changes to the remote repository for a stack.", executionContext, inputProvider, outputProvider, logger)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(CommonOptions.MaxBatchSize);
        Add(ForceWithLease);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new PushStackCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(CommonOptions.MaxBatchSize),
                parseResult.GetValue(ForceWithLease)),
            cancellationToken);
    }
}

public record PushStackCommandInputs(string? Stack, int MaxBatchSize, bool ForceWithLease)
{
    public static PushStackCommandInputs Default => new(null, 5, false);
}

public class PushStackCommandHandler(
    IInputProvider inputProvider,
    ILogger<PushStackCommandHandler> logger,
    IDisplayProvider displayProvider,
    IGitClientFactory gitClientFactory,
    CliExecutionContext executionContext,
    IStackRepository repository,
    IStackActions stackActions)
    : CommandHandlerBase<PushStackCommandInputs>
{
    public override async Task Handle(PushStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        var stacksForRemote = repository.GetStacks();

        if (stacksForRemote.Count == 0)
        {
            logger.NoStacksForRepository();
            return;
        }

        var currentBranch = gitClient.GetCurrentBranch();

        var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");

        await displayProvider.DisplayStatus($"Pushing changes to remote repository...", async (ct) =>
        {
            await Task.CompletedTask;
            stackActions.PushChanges(stack, inputs.MaxBatchSize, inputs.ForceWithLease);
        }, cancellationToken);

        logger.PushedStack(stack.Name);
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(1, LogLevel.Information, "Pushed changes for stack '{Stack}'.")]
    public static partial void PushedStack(this ILogger logger, string stack);
}
