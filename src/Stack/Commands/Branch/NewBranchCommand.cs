using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class NewBranchCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to create.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("Force creating the branch without prompting.")]
    [CommandOption("-f|--force")]
    public bool Force { get; init; }
}

public class NewBranchCommand : AsyncCommand<NewBranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, NewBranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var console = AnsiConsole.Console;

        var handler = new NewBranchCommandHandler(
            new ConsoleInputProvider(console),
            new ConsoleOutputProvider(console),
            new GitOperations(console, settings.GetGitOperationSettings()),
            new StackConfig());

        await handler.Handle(new NewBranchCommandInputs(settings.Stack, settings.Name, settings.Force));

        return 0;
    }
}

public record NewBranchCommandInputs(string? StackName, string? BranchName, bool Force)
{
    public static NewBranchCommandInputs Empty => new(null, null, false);
}

public record NewBranchCommandResponse();

public class NewBranchCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IStackConfig stackConfig)
{
    public async Task<NewBranchCommandResponse> Handle(NewBranchCommandInputs inputs)
    {
        await Task.CompletedTask;

        var defaultBranch = gitOperations.GetDefaultBranch();
        var remoteUri = gitOperations.GetRemoteUri();
        var currentBranch = gitOperations.GetCurrentBranch();
        var branches = gitOperations.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var stacks = stackConfig.Load();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            outputProvider.Information("No stacks found for current repository.");
            return new NewBranchCommandResponse();
        }

        var stack = inputProvider.SelectStack(outputProvider, inputs.StackName, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var sourceBranch = stack.Branches.LastOrDefault() ?? stack.SourceBranch;

        var branchName = inputProvider.Text(outputProvider, Questions.BranchName, inputs.BranchName, stack.GetDefaultBranchName());

        if (stack.Branches.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists in stack '{stack.Name}'.");
        }

        if (gitOperations.DoesLocalBranchExist(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists locally.");
        }

        outputProvider.Information($"Creating branch {branchName.Branch()} from {sourceBranch.Branch()} in stack {stack.Name.Stack()}");

        gitOperations.CreateNewBranch(branchName, sourceBranch);
        gitOperations.PushNewBranch(branchName);

        stack.Branches.Add(branchName);

        stackConfig.Save(stacks);

        outputProvider.Information($"Branch created");

        if (inputs.Force || inputProvider.Confirm(Questions.ConfirmSwitchToBranch))
        {
            gitOperations.ChangeBranch(branchName);
        }

        return new NewBranchCommandResponse();
    }
}