using System.CommandLine;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class PullStackCommand : Command
{
    public PullStackCommand() : base("pull", "Pull changes from the remote repository for a stack.")
    {
        Add(CommonOptions.Stack);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var gitClient = new GitClient(StdErrLogger, new GitClientSettings(Verbose, WorkingDirectory));
        var gitHubClient = new GitHubClient(StdErrLogger, new GitHubClientSettings(Verbose, WorkingDirectory));

        var handler = new PullStackCommandHandler(
            InputProvider,
            StdErrLogger,
            gitClient,
            new FileStackConfig(),
            new StackActions(gitClient, gitHubClient, InputProvider, StdErrLogger));

        await handler.Handle(
            new PullStackCommandInputs(
                parseResult.GetValue(CommonOptions.Stack)),
            cancellationToken);
    }
}

public record PullStackCommandInputs(string? Stack);
public class PullStackCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IStackConfig stackConfig,
    IStackActions stackActions)
    : CommandHandlerBase<PullStackCommandInputs>
{
    public override async Task Handle(PullStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            logger.Information("No stacks found for current repository.");
            return;
        }

        var currentBranch = gitClient.GetCurrentBranch();

        var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");

        stackActions.PullChanges(stack);

        gitClient.ChangeBranch(currentBranch);
    }
}
