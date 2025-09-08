using System.CommandLine;
using Microsoft.Extensions.Logging;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public class RenameStackCommand : Command
{
    private readonly RenameStackCommandHandler handler;

    public RenameStackCommand(
        ILogger<RenameStackCommand> logger,
        IDisplayProvider displayProvider,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        RenameStackCommandHandler handler)
        : base("rename", "Rename a stack.", logger, displayProvider, inputProvider, executionContext)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(CommonOptions.Name);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new RenameStackCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(CommonOptions.Name)),
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
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<RenameStackCommandInputs>
{
    public override async Task Handle(RenameStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

        if (stack is null)
        {
            throw new InvalidOperationException("Stack not found.");
        }

        var newName = await inputProvider.Text(logger, Questions.NewStackName, inputs.Name, cancellationToken);

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
        var stackIndex = stackData.Stacks.IndexOf(stack);
        stackData.Stacks[stackIndex] = renamedStack;
        
        stackConfig.Save(stackData);

        logger.StackRenamed(stack.Name, newName);
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Stack \"{OldName}\" renamed to \"{NewName}\"")]
    public static partial void StackRenamed(this ILogger logger, string oldName, string newName);
}