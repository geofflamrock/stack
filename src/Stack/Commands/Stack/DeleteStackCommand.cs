using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Commands.Helpers;
using System.CommandLine;
using Microsoft.Extensions.Logging;


namespace Stack.Commands;

public class DeleteStackCommand : Command
{
    private readonly DeleteStackCommandHandler handler;

    public DeleteStackCommand(
        ILogger<DeleteStackCommand> logger,
        IAnsiConsoleWriter console,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        DeleteStackCommandHandler handler)
        : base("delete", "Delete a stack.", logger, console, inputProvider, executionContext)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(CommonOptions.Confirm);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new DeleteStackCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(CommonOptions.Confirm)),
            cancellationToken);
    }
}

public record DeleteStackCommandInputs(string? Stack, bool Confirm)
{
    public static DeleteStackCommandInputs Empty => new(null, false);
}

public record DeleteStackCommandResponse(string? DeletedStackName);

public class DeleteStackCommandHandler(
    IInputProvider inputProvider,
    ILogger<DeleteStackCommandHandler> logger,
    IAnsiConsoleWriter console,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<DeleteStackCommandInputs>
{
    public override async Task Handle(DeleteStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
        {
            throw new InvalidOperationException("Stack not found.");
        }

        if (inputs.Confirm || await inputProvider.Confirm(Questions.ConfirmDeleteStack, cancellationToken))
        {
            var branchesNeedingCleanup = StackHelpers.GetBranchesNeedingCleanup(stack, logger, console, gitClient, gitHubClient);

            if (branchesNeedingCleanup.Length > 0)
            {
                StackHelpers.OutputBranchesNeedingCleanup(logger, branchesNeedingCleanup);

                if (inputs.Confirm || await inputProvider.Confirm(Questions.ConfirmDeleteBranches, cancellationToken))
                {
                    StackHelpers.CleanupBranches(gitClient, logger, branchesNeedingCleanup);
                }
            }

            stackData.Stacks.Remove(stack);
            stackConfig.Save(stackData);

            logger.LogInformation($"Stack {stack.Name.Stack()} deleted");
        }
    }
}
