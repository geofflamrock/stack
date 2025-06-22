using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Commands.Helpers;
using System.CommandLine;

namespace Stack.Commands;

public class DeleteStackCommand : Command
{
    public DeleteStackCommand() : base("delete", "Delete a stack.")
    {
        Add(CommonOptions.Stack);
        Add(CommonOptions.Confirm);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var handler = new DeleteStackCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, new GitClientSettings(Verbose, WorkingDirectory)),
            new GitHubClient(StdErrLogger, new GitHubClientSettings(Verbose, WorkingDirectory)),
            new FileStackConfig());

        await handler.Handle(new DeleteStackCommandInputs(
            parseResult.GetValue(CommonOptions.Stack),
            parseResult.GetValue(CommonOptions.Confirm)));
    }
}

public record DeleteStackCommandInputs(string? Stack, bool Confirm)
{
    public static DeleteStackCommandInputs Empty => new(null, false);
}

public record DeleteStackCommandResponse(string? DeletedStackName);

public class DeleteStackCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<DeleteStackCommandInputs>
{
    public override async Task Handle(DeleteStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        var stack = inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException("Stack not found.");
        }

        if (inputs.Confirm || inputProvider.Confirm(Questions.ConfirmDeleteStack))
        {
            var branchesNeedingCleanup = StackHelpers.GetBranchesNeedingCleanup(stack, logger, gitClient, gitHubClient);

            if (branchesNeedingCleanup.Length > 0)
            {
                StackHelpers.OutputBranchesNeedingCleanup(logger, branchesNeedingCleanup);

                if (inputs.Confirm || inputProvider.Confirm(Questions.ConfirmDeleteBranches))
                {
                    StackHelpers.CleanupBranches(gitClient, logger, branchesNeedingCleanup);
                }
            }

            stackData.Stacks.Remove(stack);
            stackConfig.Save(stackData);

            logger.Information($"Stack {stack.Name.Stack()} deleted");
        }
    }
}
