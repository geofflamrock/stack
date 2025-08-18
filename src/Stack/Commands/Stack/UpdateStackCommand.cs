using System.CommandLine;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class UpdateStackCommand : Command
{
    public UpdateStackCommand() : base("update", "Update the branches in a stack.")
    {
        Add(CommonOptions.Stack);
        Add(CommonOptions.Rebase);
        Add(CommonOptions.Merge);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var gitClient = new GitClient(StdErrLogger, new GitClientSettings(Verbose, WorkingDirectory));
        var gitHubClient = new GitHubClient(StdErrLogger, new GitHubClientSettings(Verbose, WorkingDirectory));
        var stackActions = new StackActions(
            gitClient,
            InputProvider,
            StdErrLogger);

        var handler = new UpdateStackCommandHandler(
            InputProvider,
            StdErrLogger,
            gitClient,
            gitHubClient,
            new FileStackConfig(),
            stackActions);

        await handler.Handle(new UpdateStackCommandInputs(
            parseResult.GetValue(CommonOptions.Stack),
            parseResult.GetValue(CommonOptions.Rebase),
            parseResult.GetValue(CommonOptions.Merge)));
    }
}

public record UpdateStackCommandInputs(string? Stack, bool? Rebase, bool? Merge)
{
    public static UpdateStackCommandInputs Empty => new(null, null, null);
}

public record UpdateStackCommandResponse();

public class UpdateStackCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig,
    IStackActions stackActions)
    : CommandHandlerBase<UpdateStackCommandInputs>
{
    public override async Task Handle(UpdateStackCommandInputs inputs)
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

        var updateStrategy =
            inputs.Merge == true ? UpdateStrategy.Merge :
            inputs.Rebase == true ? UpdateStrategy.Rebase :
            stackActions.GetUpdateStrategyConfigValue();

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

        var status = StackHelpers.GetStackStatus(
            stack,
            gitClient.GetCurrentBranch(),
            logger,
            gitClient,
            gitHubClient,
            false);

        stackActions.UpdateStack(
            stack,
            updateStrategy.Value,
            status);

        if (stack.SourceBranch.Equals(currentBranch, StringComparison.InvariantCultureIgnoreCase) ||
            stack.AllBranchNames.Contains(currentBranch, StringComparer.OrdinalIgnoreCase))
        {
            gitClient.ChangeBranch(currentBranch);
        }

        return;
    }
}