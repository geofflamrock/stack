using System.CommandLine;
using Microsoft.Extensions.Logging;
using Stack.Commands.Helpers;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;
using Stack.Persistence;

namespace Stack.Commands;

public class RenameStackCommand : Command
{
    static new readonly Option<string?> Name = new("--name", "-n")
    {
        Description = "The new name for the stack.",
        Required = false
    };

    private readonly RenameStackCommandHandler handler;

    public RenameStackCommand(
        RenameStackCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<RenameStackCommand> logger)
        : base("rename", "Rename a stack.", executionContext, inputProvider, outputProvider, logger)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(Name);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new RenameStackCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(Name)),
            cancellationToken);
    }
}

public record RenameStackCommandInputs(string? Stack, string? Name)
{
    public static RenameStackCommandInputs Empty => new(null, null);
}

public record RenameStackCommandResponse();

public class RenameStackCommandHandler(
    IInputProvider inputProvider,
    ILogger<RenameStackCommandHandler> logger,
    IGitClientFactory gitClientFactory,
    CliExecutionContext executionContext,
    IStackRepository repository)
    : CommandHandlerBase<RenameStackCommandInputs>
{
    public override async Task Handle(RenameStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        var gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = repository.GetStacks();

        if (stacksForRemote.Count == 0)
        {
            logger.NoStacksForRepository();
            return;
        }

        var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
        {
            throw new InvalidOperationException("Stack not found.");
        }

        var newName = await inputProvider.Text(logger, Questions.StackName, inputs.Name, cancellationToken);

        // Validate that there's not another stack with the same name for the same remote
        var existingStackWithSameName = stacksForRemote.FirstOrDefault(s =>
            s.Name.Equals(newName, StringComparison.OrdinalIgnoreCase) &&
            !s.Name.Equals(stack.Name, StringComparison.OrdinalIgnoreCase));

        if (existingStackWithSameName is not null)
        {
            throw new InvalidOperationException($"A stack with the name '{newName}' already exists for this remote.");
        }

        var renamedStack = stack.ChangeName(newName);

        // Update the stack in the collection
        repository.RemoveStack(stack);
        repository.AddStack(renamedStack);
        repository.SaveChanges();

        logger.StackRenamed(stack.Name, newName);
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Stack \"{OldName}\" renamed to \"{NewName}\"")]
    public static partial void StackRenamed(this ILogger logger, string oldName, string newName);
}