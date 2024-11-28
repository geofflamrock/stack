using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class RemoveBranchCommandSettings : DryRunCommandSettingsBase
{
    [Description("The name of the stack to create the branch in.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("The name of the branch to add.")]
    [CommandOption("-n|--name")]
    public string? Name { get; init; }

    [Description("Force removing the branch without prompting.")]
    [CommandOption("-f|--force")]
    public bool Force { get; init; }
}

public class RemoveBranchCommand : AsyncCommand<RemoveBranchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, RemoveBranchCommandSettings settings)
    {
        await Task.CompletedTask;

        var console = AnsiConsole.Console;

        var handler = new RemoveBranchCommandHandler(
            new ConsoleInputProvider(console),
            new ConsoleOutputProvider(console),
            new GitOperations(console, settings.GetGitOperationSettings()),
            new StackConfig());

        await handler.Handle(new RemoveBranchCommandInputs(settings.Stack, settings.Name, settings.Force));

        return 0;
    }
}

public record RemoveBranchCommandInputs(string? StackName, string? BranchName, bool Force)
{
    public static RemoveBranchCommandInputs Empty => new(null, null, false);
}

public record RemoveBranchCommandResponse();

public class RemoveBranchCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitOperations gitOperations,
    IStackConfig stackConfig)
{
    public async Task<RemoveBranchCommandResponse> Handle(RemoveBranchCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri();
        var currentBranch = gitOperations.GetCurrentBranch();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();
        var stack = InputHelpers.SelectStack(inputProvider, outputProvider, inputs.StackName, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.StackName}' not found.");
        }

        var branchName = inputs.BranchName ?? inputProvider.Select(Questions.SelectBranch, [.. stack.Branches]);

        if (!stack.Branches.Contains(branchName))
        {
            throw new InvalidOperationException($"Branch '{branchName}' not found in stack '{stack.Name}'.");
        }

        if (inputs.Force || inputProvider.Confirm(Questions.ConfirmRemoveBranch(stack.Name, branchName)))
        {
            stack.Branches.Remove(branchName);
            stackConfig.Save(stacks);

            outputProvider.Information($"Branch {branchName.Branch()} removed from stack {stack.Name.Stack()}");

            return new RemoveBranchCommandResponse();
        }

        return new RemoveBranchCommandResponse();
    }
}

