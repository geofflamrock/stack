using System.CommandLine;
using Microsoft.Extensions.Logging;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class UpdateStackCommand : Command
{
    private readonly UpdateStackCommandHandler handler;

    public UpdateStackCommand(
        UpdateStackCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<UpdateStackCommand> logger)
        : base("update", "Update the branches in a stack.", executionContext, inputProvider, outputProvider, logger)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(CommonOptions.Rebase);
        Add(CommonOptions.Merge);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new UpdateStackCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(CommonOptions.Rebase),
                parseResult.GetValue(CommonOptions.Merge)),
            cancellationToken);
    }
}

public record UpdateStackCommandInputs(string? Stack, bool? Rebase, bool? Merge)
{
    public static UpdateStackCommandInputs Empty => new(null, null, null);
}

public record UpdateStackCommandResponse();

public class UpdateStackCommandHandler(
    IInputProvider inputProvider,
    ILogger<UpdateStackCommandHandler> logger,
    IDisplayProvider displayProvider,
    IGitClientFactory gitClientFactory,
    CliExecutionContext executionContext,
    IStackConfig stackConfig,
    IStackActions stackActions)
    : CommandHandlerBase<UpdateStackCommandInputs>
{
    public override async Task Handle(UpdateStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        if (inputs.Rebase == true && inputs.Merge == true)
            throw new InvalidOperationException("Cannot specify both rebase and merge.");

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

        var updateStrategy = await StackHelpers.GetUpdateStrategy(
            inputs.Merge == true ? UpdateStrategy.Merge : inputs.Rebase == true ? UpdateStrategy.Rebase : null,
            gitClient, inputProvider, logger, cancellationToken);

        await displayProvider.DisplayStatus("Updating stack...", async (ct) =>
        {
            await stackActions.UpdateStack(stack, updateStrategy, cancellationToken);
        }, cancellationToken);

        if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
            stack.AllBranchNames.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
        {
            gitClient.ChangeBranch(currentBranch);
        }
    }
}