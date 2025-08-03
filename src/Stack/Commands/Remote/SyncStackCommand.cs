using System.CommandLine;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class SyncStackCommand : Command
{
    readonly Option<bool> NoPush = new("--no-push")
    {
        Description = "Don't push changes to the remote repository"
    };

    public SyncStackCommand() : base("sync", "Sync a stack with the remote repository. Shortcut for `git fetch --prune`, `stack pull`, `stack update` and `stack push`.")
    {
        Add(CommonOptions.Stack);
        Add(CommonOptions.MaxBatchSize);
        Add(CommonOptions.Rebase);
        Add(CommonOptions.Merge);
        Add(CommonOptions.Confirm);
        Add(NoPush);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var handler = new SyncStackCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, new GitClientSettings(Verbose, WorkingDirectory)),
            new GitHubClient(StdErrLogger, new GitHubClientSettings(Verbose, WorkingDirectory)),
            new FileStackConfig(),
            new StackActions(),
            new StackActions());

        await handler.Handle(new SyncStackCommandInputs(
            parseResult.GetValue(CommonOptions.Stack),
            parseResult.GetValue(CommonOptions.MaxBatchSize),
            parseResult.GetValue(CommonOptions.Rebase),
            parseResult.GetValue(CommonOptions.Merge),
            parseResult.GetValue(CommonOptions.Confirm),
            parseResult.GetValue(NoPush)));
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
    ILocalStackActions localStackActions,
    IRemoteStackActions remoteStackActions)
    : CommandHandlerBase<SyncStackCommandInputs>
{
    public override async Task Handle(SyncStackCommandInputs inputs)
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

        var stack = inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch);

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

        StackHelpers.OutputStackStatus(stackData.SchemaVersion, status, logger);

        logger.NewLine();

        if (inputs.Confirm || inputProvider.Confirm(Questions.ConfirmSyncStack))
        {
            logger.Information($"Syncing stack {stack.Name.Stack()} with the remote repository");

            remoteStackActions.PullChanges(stack, gitClient, logger);

            var updateStrategy =
                inputs.Merge == true ? UpdateStrategy.Merge :
                inputs.Rebase == true ? UpdateStrategy.Rebase :
                localStackActions.GetUpdateStrategyConfigValue(gitClient);

            if (updateStrategy == null)
            {
                updateStrategy = inputProvider.Select(
                    Questions.SelectUpdateStrategy,
                    [UpdateStrategy.Merge, UpdateStrategy.Rebase]);

                logger.Information($"{Questions.SelectUpdateStrategy} {updateStrategy}");

                logger.NewLine();
                logger.Information($"Run {$"git config stack.update.strategy {updateStrategy.ToString()!.ToLowerInvariant()}".Example()} to configure this update strategy for the current repository.");
                logger.Information($"Run {$"git config --global stack.update.strategy {updateStrategy.ToString()!.ToLowerInvariant()}".Example()} to configure this update strategy for all repositories.");
                logger.NewLine();
            }

            localStackActions.UpdateStack(
                stack,
                status,
                updateStrategy,
                gitClient,
                inputProvider,
                logger);

            var forceWithLease = updateStrategy == UpdateStrategy.Rebase;

            if (!inputs.NoPush)
                remoteStackActions.PushChanges(stack, inputs.MaxBatchSize, forceWithLease, gitClient, logger);

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