using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class AddBranchCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to add.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }
}

public class AddBranchCommand : AsyncCommand<AddBranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AddBranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var console = AnsiConsole.Console;
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new AddBranchCommandHandler(
            new ConsoleInputProvider(console),
            outputProvider,
            new GitClient(outputProvider, settings.GetGitClientSettings()),
            new StackConfig());

        await handler.Handle(new AddBranchCommandInputs(settings.Stack, settings.Name));

        return 0;
    }
}

public record AddBranchCommandInputs(string? StackName, string? BranchName)
{
    public static AddBranchCommandInputs Empty => new(null, null);
}

public record AddBranchCommandResponse();

public class AddBranchCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitClient gitClient,
    IStackConfig stackConfig)
{
    public async Task<AddBranchCommandResponse> Handle(AddBranchCommandInputs inputs)
    {
        await Task.CompletedTask;

        var remoteUri = gitClient.GetRemoteUri();
        var currentBranch = gitClient.GetCurrentBranch();
        var branches = gitClient.GetLocalBranchesOrderedByMostRecentCommitterDate();

        var stacks = stackConfig.Load();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            outputProvider.Information("No stacks found for current repository.");
            return new AddBranchCommandResponse();
        }

        var stack = inputProvider.SelectStack(outputProvider, inputs.StackName, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var sourceBranch = stack.Branches.LastOrDefault() ?? stack.SourceBranch;
        var branchName = inputProvider.SelectBranch(outputProvider, inputs.BranchName, branches);

        if (stack.Branches.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' already exists in stack '{stack.Name}'.");
        }

        if (!gitClient.DoesLocalBranchExist(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' does not exist locally.");
        }

        outputProvider.Information($"Adding branch {branchName.Branch()} to stack {stack.Name.Stack()}");

        stack.Branches.Add(branchName);

        stackConfig.Save(stacks);

        outputProvider.Information($"Branch added");

        return new AddBranchCommandResponse();
    }
}