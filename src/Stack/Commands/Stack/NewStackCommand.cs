

using System.CommandLine;
using System.ComponentModel;
using Humanizer;
using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

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

    public NewStackCommand() : base("new", "Create a new stack.")
    {
        Add(StackName);
        Add(SourceBranch);
        Add(BranchName);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var handler = new NewStackCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, new GitClientSettings(Verbose, WorkingDirectory)),
            new FileStackConfig());

        await handler.Handle(new NewStackCommandInputs(
            parseResult.GetValue(StackName),
            parseResult.GetValue(SourceBranch),
            parseResult.GetValue(BranchName)));
    }
}

public record NewStackCommandInputs(string? Name, string? SourceBranch, string? BranchName)
{
    public static NewStackCommandInputs Empty => new(null, null, null);
}

public class NewStackCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<NewStackCommandInputs>
{
    public override async Task Handle(NewStackCommandInputs inputs)
    {
        await Task.CompletedTask;

        var name = inputProvider.Text(logger, Questions.StackName, inputs.Name);

        var branches = gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var sourceBranch = inputProvider.Select(logger, Questions.SelectSourceBranch, inputs.SourceBranch, branches);

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
            var selectedBranchAction = inputProvider.Select(
                Questions.AddOrCreateBranch,
                [BranchAction.Create, BranchAction.Add, BranchAction.None],
                action => action.Humanize());

            logger.Information($"{Questions.AddOrCreateBranch} {selectedBranchAction.Humanize()}");
            branchAction = selectedBranchAction;
        }

        if (branchAction == BranchAction.Create)
        {
            branchName = inputProvider.Text(logger, Questions.BranchName, inputs.BranchName, stack.GetDefaultBranchName());

            gitClient.CreateNewBranch(branchName, sourceBranch);

            try
            {
                logger.Information($"Pushing branch {branchName.Branch()} to remote repository");
                gitClient.PushNewBranch(branchName);
            }
            catch (Exception)
            {
                logger.Warning($"An error has occurred pushing branch {branchName.Branch()} to remote repository. Use {$"stack push --name \"{name}\"".Example()} to push the branch to the remote repository.");
            }
        }
        else if (branchAction == BranchAction.Add)
        {
            branchName = inputs.BranchName ?? inputProvider.SelectBranch(logger, null, branches);
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
                logger.Warning($"An error has occurred changing to branch {branchName.Branch()}. Use {$"stack switch --branch \"{branchName}\"".Example()} to switch to the branch. Error: {ex.Message}");
            }
        }

        if (branchAction is BranchAction.Create)
        {
            logger.Information($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()} with new branch {branchName!.Branch()}");
        }
        else if (branchAction is BranchAction.Add)
        {
            logger.Information($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()} with existing branch {branchName!.Branch()}");
        }
        else
        {
            logger.Information($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()}");
        }
    }
}


