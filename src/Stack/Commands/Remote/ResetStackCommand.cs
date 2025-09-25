using System.CommandLine;
using Microsoft.Extensions.Logging;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class ResetStackCommand : Command
{
    private readonly ResetStackCommandHandler handler;

    public ResetStackCommand(
        ResetStackCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<ResetStackCommand> logger)
        : base("reset", "Reset all branches in a stack to match their remote tracking branches.", executionContext, inputProvider, outputProvider, logger)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(CommonOptions.Confirm);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new ResetStackCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(CommonOptions.Confirm)),
            cancellationToken);
    }
}

public record ResetStackCommandInputs(string? Stack, bool Confirm);

public class ResetStackCommandHandler(
    IInputProvider inputProvider,
    ILogger<ResetStackCommandHandler> logger,
    IDisplayProvider displayProvider,
    IOutputProvider outputProvider,
    IGitClientFactory gitClientFactory,
    CliExecutionContext executionContext,
    IStackConfig stackConfig,
    IGitHubClient gitHubClient,
    IStackActions stackActions)
    : CommandHandlerBase<ResetStackCommandInputs>
{
    public override async Task Handle(ResetStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        var stackData = stackConfig.Load();

        var gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        var remoteUri = gitClient.GetRemoteUri();
        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            logger.NoStacksForRepository();
            return;
        }

        var currentBranch = gitClient.GetCurrentBranch();

        var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");

        await displayProvider.DisplayStatusWithSuccess("Fetching changes from remote repository...", async ct =>
        {
            await Task.CompletedTask;
            gitClient.Fetch(false);
        }, cancellationToken);

        if (!inputs.Confirm)
        {
            var status = await displayProvider.DisplayStatus("Checking stack status...", async ct =>
            {
                await Task.CompletedTask;
                return StackHelpers.GetStackStatus(
                    stack,
                    currentBranch,
                    logger,
                    gitClient,
                    gitHubClient,
                    false);
            }, cancellationToken);

            await StackHelpers.OutputStackStatus(status, outputProvider, cancellationToken);

            if (!await inputProvider.Confirm(Questions.ConfirmResetStack, cancellationToken, false))
            {
                return;
            }
        }

        await displayProvider.DisplayStatus("Resetting stack branches to remote...", async ct =>
        {
            await stackActions.ResetStack(stack, ct);
        }, cancellationToken);

        gitClient.ChangeBranch(currentBranch);

        logger.StackReset(stack.Name);
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(3, LogLevel.Information, "Reset stack '{Stack}'.")]
    public static partial void StackReset(this ILogger logger, string stack);
}
