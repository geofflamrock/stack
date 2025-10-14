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
        DeleteStackCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<DeleteStackCommand> logger)
        : base("delete", "Delete a stack.", executionContext, inputProvider, outputProvider, logger)
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
    IGitClientFactory gitClientFactory,
    CliExecutionContext executionContext,
    IGitHubClient gitHubClient,
    IStackRepository repository)
    : CommandHandlerBase<DeleteStackCommandInputs>
{
    public override async Task Handle(DeleteStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = repository.GetStacks();

        var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
        {
            throw new InvalidOperationException("Stack not found.");
        }

        if (inputs.Confirm || await inputProvider.Confirm(Questions.ConfirmDeleteStack, cancellationToken))
        {
            var branchesNeedingCleanup = StackHelpers.GetBranchesNeedingCleanup(stack, logger, gitClient, gitHubClient);

            if (branchesNeedingCleanup.Length > 0)
            {
                StackHelpers.OutputBranchesNeedingCleanup(logger, branchesNeedingCleanup);

                if (inputs.Confirm || await inputProvider.Confirm(Questions.ConfirmDeleteBranches, cancellationToken))
                {
                    StackHelpers.CleanupBranches(gitClient, logger, branchesNeedingCleanup);
                }
            }

            repository.RemoveStack(stack);
            repository.SaveChanges();

            logger.StackDeleted(stack.Name);
        }
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Stack \"{Stack}\" deleted")]
    public static partial void StackDeleted(this ILogger logger, string stack);
}
