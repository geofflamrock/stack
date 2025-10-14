using System.CommandLine;
using Microsoft.Extensions.Logging;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class PullStackCommand : Command
{
    private readonly PullStackCommandHandler handler;

    public PullStackCommand(
        PullStackCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<PullStackCommand> logger)
        : base("pull", "Pull changes from the remote repository for a stack.", executionContext, inputProvider, outputProvider, logger)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new PullStackCommandInputs(
                parseResult.GetValue(CommonOptions.Stack)),
            cancellationToken);
    }
}

public record PullStackCommandInputs(string? Stack);
public class PullStackCommandHandler(
    IInputProvider inputProvider,
    ILogger<PullStackCommandHandler> logger,
    IDisplayProvider displayProvider,
    IGitClientFactory gitClientFactory,
    CliExecutionContext executionContext,
    IStackRepository repository,
    IStackActions stackActions)
    : CommandHandlerBase<PullStackCommandInputs>
{
    public override async Task Handle(PullStackCommandInputs inputs, CancellationToken cancellationToken)
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

        await displayProvider.DisplayStatus($"Pulling changes from remote repository...", async (ct) =>
        {
            await Task.CompletedTask;
            stackActions.PullChanges(stack);
        }, cancellationToken);

        gitClient.ChangeBranch(currentBranch);

        logger.PulledStack(stack.Name);
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(2, LogLevel.Information, "Pulled changes for stack '{Stack}'.")]
    public static partial void PulledStack(this ILogger logger, string stack);
}
