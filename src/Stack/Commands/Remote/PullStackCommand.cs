using System.CommandLine;

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
        IStdOutLogger stdOutLogger,
        IStdErrLogger stdErrLogger,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        PullStackCommandHandler handler)
    : base("pull", "Pull changes from the remote repository for a stack.", stdOutLogger, stdErrLogger, inputProvider, executionContext)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(new PullStackCommandInputs(
            parseResult.GetValue(CommonOptions.Stack)));
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
    public override async Task Handle(PullStackCommandInputs inputs)
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

        var stack = inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");

        stackActions.PullChanges(stack);

        gitClient.ChangeBranch(currentBranch);
    }
}
