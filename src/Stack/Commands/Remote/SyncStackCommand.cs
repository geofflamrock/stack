using System.CommandLine;

using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class SyncStackCommand : Command
{
    readonly Option<bool> NoPush = new("--no-push")
    {
        Description = "Don't push changes to the remote repository"
    };

    private readonly SyncStackCommandHandler handler;

    public SyncStackCommand(
        IStdOutLogger stdOutLogger,
        IStdErrLogger stdErrLogger,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        SyncStackCommandHandler handler)
    : base("sync", "Sync a stack with the remote repository. Shortcut for `git fetch --prune`, `stack pull`, `stack update` and `stack push`.", stdOutLogger, stdErrLogger, inputProvider, executionContext)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(CommonOptions.MaxBatchSize);
        Add(CommonOptions.Rebase);
        Add(CommonOptions.Merge);
        Add(CommonOptions.Confirm);
        Add(NoPush);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new SyncStackCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(CommonOptions.MaxBatchSize),
                parseResult.GetValue(CommonOptions.Rebase),
                parseResult.GetValue(CommonOptions.Merge),
                parseResult.GetValue(CommonOptions.Confirm),
                parseResult.GetValue(NoPush)),
            cancellationToken);
    }
}

public record SyncStackCommandInputs(
    string? Stack,
    int MaxBatchSize,
    bool? Rebase,
    bool? Merge,
    bool Confirm,
    bool NoPush)
{
    public static SyncStackCommandInputs Empty => new(null, 5, null, null, false, false);
}

public class SyncStackCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig,
    IStackActions stackActions)
    : CommandHandlerBase<SyncStackCommandInputs>
{
    public override async Task Handle(SyncStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        if (inputs.Rebase == true && inputs.Merge == true)
            throw new InvalidOperationException("Cannot specify both rebase and merge.");

        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            return;
        }

        var currentBranch = gitClient.GetCurrentBranch();

        var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");

        FetchChanges();

        var status = StackHelpers.GetStackStatus(
            stack,
            currentBranch,
            logger,
            gitClient,
            gitHubClient,
            true);

        StackHelpers.OutputStackStatus(status, logger);

        logger.NewLine();

        if (inputs.Confirm || await inputProvider.Confirm(Questions.ConfirmSyncStack, cancellationToken))
        {
            logger.Information($"Syncing stack {stack.Name.Stack()} with the remote repository");

            stackActions.PullChanges(stack);

            var updateStrategy = await StackHelpers.GetUpdateStrategy(
                inputs.Merge == true ? UpdateStrategy.Merge : inputs.Rebase == true ? UpdateStrategy.Rebase : null,
                gitClient, inputProvider, logger, cancellationToken);

            await stackActions.UpdateStack(stack, updateStrategy, cancellationToken);

            var forceWithLease = updateStrategy == UpdateStrategy.Rebase;

            if (!inputs.NoPush)
                stackActions.PushChanges(stack, inputs.MaxBatchSize, forceWithLease);

            if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
                stack.AllBranchNames.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
            {
                gitClient.ChangeBranch(currentBranch);
            }
        }
    }

    private void FetchChanges()
    {
        logger.Status("Fetching changes from remote repository", () =>
        {
            gitClient.Fetch(true);
        });
    }
}