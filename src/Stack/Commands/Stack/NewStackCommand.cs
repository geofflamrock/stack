

using System.ComponentModel;
using Humanizer;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class NewStackCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack. Must be unique.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("The source branch to use for the new branch. Defaults to the default branch for the repository.")]
    [CommandOption("--source-branch")]
    public string? SourceBranch { get; init; }

    [Description("The name of a new or existing branch to add to the stack.")]
    [CommandOption("-b|--branch")]
    public string? BranchName { get; init; }

    [Description("Confirm all prompts")]
    [CommandOption("--yes")]
    public bool Yes { get; init; }
}

public enum BranchAction
{
    None,

    [Description("Add an existing branch")]
    Add,

    [Description("Create a new branch")]
    Create
}

public class NewStackCommand : AsyncCommand<NewStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NewStackCommandSettings settings)
    {
        var console = AnsiConsole.Console;
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new NewStackCommandHandler(
            new ConsoleInputProvider(console),
            outputProvider,
            new GitClient(outputProvider, settings.GetGitClientSettings()),
            new StackConfig());

        await handler.Handle(
            new NewStackCommandInputs(settings.Name, settings.SourceBranch, settings.BranchName, settings.Yes));

        return 0;
    }
}

public record NewStackCommandInputs(string? Name, string? SourceBranch, string? BranchName, bool Yes = false)
{
    public static NewStackCommandInputs Empty => new(null, null, null, false);
}

public record NewStackCommandResponse(string StackName, string SourceBranch, BranchAction? BranchAction, string? BranchName);

public class NewStackCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitClient gitClient,
    IStackConfig stackConfig)
{
    public async Task<NewStackCommandResponse> Handle(NewStackCommandInputs inputs)
    {
        await Task.CompletedTask;

        var name = inputProvider.Text(outputProvider, Questions.StackName, inputs.Name);

        var branches = gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var sourceBranch = inputProvider.Select(outputProvider, Questions.SelectSourceBranch, inputs.SourceBranch, branches);

        var stacks = stackConfig.Load();
        var remoteUri = gitClient.GetRemoteUri();
        var stack = new Config.Stack(name, remoteUri, sourceBranch, []);
        string? branchName = null;
        BranchAction branchAction = BranchAction.None;

        if (inputs.BranchName is not null)
        {
            branchAction = branches.Contains(inputs.BranchName) ? BranchAction.Add : BranchAction.Create;
        }
        else if (!inputs.Yes && inputProvider.Confirm(Questions.ConfirmAddOrCreateBranch))
        {
            branchAction = inputProvider.Select(
                Questions.AddOrCreateBranch,
                [BranchAction.Create, BranchAction.Add],
                action => action.Humanize());

            outputProvider.Information($"{Questions.AddOrCreateBranch} {branchAction.Humanize()}");
        }

        if (branchAction == BranchAction.Create)
        {
            branchName = inputProvider.Text(outputProvider, Questions.BranchName, inputs.BranchName, stack.GetDefaultBranchName());

            gitClient.CreateNewBranch(branchName, sourceBranch);

            if (inputs.Yes || inputProvider.Confirm(Questions.ConfirmPushBranch))
            {
                try
                {
                    gitClient.PushNewBranch(branchName);
                }
                catch (Exception)
                {
                    outputProvider.Warning($"An error has occurred pushing branch {branchName.Branch()} to remote repository. Use {$"stack push --name \"{name}\"".Example()} to push the branch to the remote repository.");
                }
            }
            else
            {
                outputProvider.Information($"Use {$"stack push --name \"{name}\"".Example()} to push the branch to the remote repository.");
            }
        }
        else if (branchAction == BranchAction.Add)
        {
            branchName = inputs.BranchName ?? inputProvider.SelectBranch(outputProvider, null, branches);
        }

        if (branchName is not null)
        {
            stack.Branches.Add(branchName);
        }

        stacks.Add(stack);

        stackConfig.Save(stacks);

        if (branchName is not null && (inputs.Yes
            || inputs.BranchName is not null
            || inputProvider.Confirm(Questions.ConfirmSwitchToBranch)))
        {
            gitClient.ChangeBranch(branchName);
        }

        if (branchAction is BranchAction.Create)
        {
            outputProvider.Information($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()} with new branch {branchName!.Branch()}");
        }
        else if (branchAction is BranchAction.Add)
        {
            outputProvider.Information($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()} with existing branch {branchName!.Branch()}");
        }
        else
        {
            outputProvider.Information($"Stack {name.Stack()} created from source branch {sourceBranch.Branch()}");
        }

        return new NewStackCommandResponse(name, sourceBranch, branchAction, branchName);
    }
}


