
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
        ILogger<NewStackCommand> logger,
        IAnsiConsoleWriter console,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        NewStackCommandHandler handler)
        : base("new", "Create a new stack.", logger, console, inputProvider, executionContext)
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
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<NewStackCommandInputs>
{
    public override async Task Handle(NewStackCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var name = await inputProvider.Text(logger, Questions.StackName, inputs.Name, cancellationToken);

        var branches = gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var sourceBranch = await inputProvider.Select(logger, Questions.SelectSourceBranch, inputs.SourceBranch, branches, cancellationToken);

        var stackData = stackConfig.Load();
        var remoteUri = gitClient.GetRemoteUri();
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

            logger.LogInformation($"{Questions.AddOrCreateBranch} {selectedBranchAction.Humanize()}");
            branchAction = selectedBranchAction;
        }

        if (branchAction == BranchAction.Create)
        {
            branchName = await inputProvider.Text(logger, Questions.BranchName, inputs.BranchName, cancellationToken);

            gitClient.CreateNewBranch(branchName, sourceBranch);

            try
            {
                logger.LogInformation($"Pushing branch {branchName.Branch()} to remote repository");
                gitClient.PushNewBranch(branchName);
            }
            catch (Exception)
            {
                logger.LogWarning($"An error has occurred pushing branch {branchName.Branch()} to remote repository. Use {$"stack push --name \"{name}\"".Example()} to push the branch to the remote repository.");
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

        stackData.Stacks.Add(stack);

        stackConfig.Save(stackData);

        if (branchName is not null)
        {
            try
            {
                gitClient.ChangeBranch(branchName);
            }
            catch (Exception ex)
            {
                logger.LogWarning($"An error has occurred changing to branch {branchName.Branch()}. Use {$"stack switch --branch \"{branchName}\"".Example()} to switch to the branch. Error: {ex.Message}");
            }
        }

        if (branchAction is BranchAction.Create)
        {
            logger.LogInformation($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()} with new branch {branchName!.Branch()}");
        }
        else if (branchAction is BranchAction.Add)
        {
            logger.LogInformation($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()} with existing branch {branchName!.Branch()}");
        }
        else
        {
            logger.LogInformation($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()}");
        }
    }
}


