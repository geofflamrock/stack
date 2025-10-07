
using System.CommandLine;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

public enum BranchAction
{
    [Description("Add an existing branch")]
    Add,

    [Description("Create a new branch")]
    Create,

    [Description("Do not add or create a branch")]
    None
}

public class NewStackCommand : Command
{
    static readonly Option<string?> StackName = new("--name", "-n")
    {
        Description = "The name of the stack. Must be unique within the repository."
    };

    static readonly Option<string?> SourceBranch = new("--source-branch", "-s")
    {
        Description = "The source branch to use for the new stack. Defaults to the default branch for the repository."
    };

    static readonly Option<string?> BranchName = new("--branch", "-b")
    {
        Description = "The name of the branch to create within the stack."
    };

    private readonly NewStackCommandHandler handler;

    public NewStackCommand(
        NewStackCommandHandler handler,
        CliExecutionContext executionContext,
        IInputProvider inputProvider,
        IOutputProvider outputProvider,
        ILogger<NewStackCommand> logger)
        : base("new", "Create a new stack.", executionContext, inputProvider, outputProvider, logger)
    {
        this.handler = handler;
        Add(StackName);
        Add(SourceBranch);
        Add(BranchName);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(
            new NewStackCommandInputs(
                parseResult.GetValue(StackName),
                parseResult.GetValue(SourceBranch),
                parseResult.GetValue(BranchName)),
            cancellationToken);
    }
}

public record NewStackCommandInputs(string? Name, string? SourceBranch, string? BranchName)
{
    public static NewStackCommandInputs Empty => new(null, null, null);
}

public class NewStackCommandHandler(
    IInputProvider inputProvider,
    ILogger<NewStackCommandHandler> logger,
    IDisplayProvider displayProvider,
    IGitClientFactory gitClientFactory,
    CliExecutionContext executionContext,
    IStackRepository repository)
    : CommandHandlerBase<NewStackCommandInputs>
{
    public override async Task Handle(NewStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var gitClient = gitClientFactory.Create(executionContext.WorkingDirectory);
        var name = await inputProvider.Text(logger, Questions.StackName, inputs.Name, cancellationToken);

        var branches = gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var sourceBranch = await inputProvider.Select(logger, Questions.SelectSourceBranch, inputs.SourceBranch, branches, cancellationToken);

        var remoteUri = repository.RemoteUri;
        var stack = new Config.Stack(name, remoteUri, sourceBranch, []);
        string? branchName = null;
        BranchAction? branchAction = null;

        if (inputs.BranchName is not null)
        {
            branchAction = branches.Contains(inputs.BranchName) ? BranchAction.Add : BranchAction.Create;
        }
        else
        {
            var selectedBranchAction = await inputProvider.Select(
                Questions.AddOrCreateBranch,
                new[] { BranchAction.Create, BranchAction.Add, BranchAction.None },
                cancellationToken,
                action => action.Humanize());

            logger.Answer(Questions.AddOrCreateBranch, selectedBranchAction.Humanize());
            branchAction = selectedBranchAction;
        }

        if (branchAction == BranchAction.Create)
        {
            branchName = await inputProvider.Text(logger, Questions.BranchName, inputs.BranchName, cancellationToken);

            gitClient.CreateNewBranch(branchName, sourceBranch);

            try
            {
                await displayProvider.DisplayStatus($"Pushing branch '{branchName}' to remote repository...", async (ct) =>
            {
                await Task.CompletedTask;
                gitClient.PushNewBranch(branchName);
            }, cancellationToken);
            }
            catch (Exception)
            {
                logger.NewBranchPushWarning(branchName, name);
            }
        }
        else if (branchAction == BranchAction.Add)
        {
            branchName = inputs.BranchName ?? await inputProvider.SelectBranch(logger, null, branches, cancellationToken);
        }

        if (branchName is not null)
        {
            stack.Branches.Add(new Branch(branchName, []));
        }

        repository.AddStack(stack);
        repository.SaveChanges();

        if (branchName is not null)
        {
            try
            {
                gitClient.ChangeBranch(branchName);
            }
            catch (Exception ex)
            {
                logger.ChangeBranchWarning(branchName, ex.Message);
            }
        }

        if (branchAction is BranchAction.Create)
        {
            logger.NewStackWithNewBranch(name, sourceBranch, branchName!);
        }
        else if (branchAction is BranchAction.Add)
        {
            logger.NewStackWithExistingBranch(name, sourceBranch, branchName!);
        }
        else
        {
            logger.NewStackWithNoBranch(name, sourceBranch);
        }
    }
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Stack \"{Stack}\" created from source branch {SourceBranch} with new branch {Branch}.")]
    public static partial void NewStackWithNewBranch(this ILogger logger, string stack, string sourceBranch, string branch);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stack \"{Stack}\" created from source branch {SourceBranch} with existing branch {Branch}.")]
    public static partial void NewStackWithExistingBranch(this ILogger logger, string stack, string sourceBranch, string branch);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stack \"{Stack}\" created from source branch {SourceBranch}.")]
    public static partial void NewStackWithNoBranch(this ILogger logger, string stack, string sourceBranch);
}


