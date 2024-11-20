using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class StackSwitchCommandSettings : CommandSettingsBase
{
    [Description("The name of the branch to switch to.")]
    [CommandOption("-b|--branch")]
    public string? Branch { get; init; }
}

public class StackSwitchCommand(
    IAnsiConsole console,
    IGitOperations gitOperations,
    IStackConfig stackConfig) : AsyncCommand<StackSwitchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StackSwitchCommandSettings settings)
    {
        await Task.CompletedTask;

        var handler = new StackSwitchCommandHandler(
            new StackSwitchCommandInputProvider(new ConsoleInputProvider(console)),
            gitOperations,
            stackConfig);

        await handler.Handle(
            new StackSwitchCommandInputs(settings.Branch),
            settings.GetGitOperationSettings());

        return 0;
    }
}

public record StackSwitchCommandInputs(string? Branch);

public record StackSwitchCommandResponse();

public interface IStackSwitchCommandInputProvider
{
    string SelectBranch(List<Config.Stack> stacks, string currentBranch);
}

public class StackSwitchCommandInputProvider(IInputProvider inputProvider) : IStackSwitchCommandInputProvider
{
    const string SelectBranchPrompt = "Select branch:";

    public string SelectBranch(List<Config.Stack> stacks, string currentBranch)
    {
        return inputProvider.SelectGrouped(
            SelectBranchPrompt,
            stacks
                .OrderByCurrentStackThenByName(currentBranch)
                .Select(s => new ChoiceGroup<string>(s.Name, [s.SourceBranch, .. s.Branches]))
                .ToArray());
    }
}

public class StackSwitchCommandHandler(IStackSwitchCommandInputProvider inputProvider, IGitOperations gitOperations, IStackConfig stackConfig)
{
    public async Task<StackSwitchCommandResponse> Handle(
        StackSwitchCommandInputs inputs,
        GitOperationSettings gitOperationSettings)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri(gitOperationSettings);
        var currentBranch = gitOperations.GetCurrentBranch(gitOperationSettings);

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        var branchSelection = inputs.Branch ?? inputProvider.SelectBranch(stacksForRemote, currentBranch);

        if (inputs.Branch is not null && !gitOperations.DoesLocalBranchExist(branchSelection, gitOperationSettings))
            throw new InvalidOperationException($"Branch '{branchSelection}' does not exist.");

        gitOperations.ChangeBranch(branchSelection, gitOperationSettings);

        return new();
    }
}