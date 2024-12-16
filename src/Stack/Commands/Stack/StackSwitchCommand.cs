using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
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

public class StackSwitchCommand : AsyncCommand<StackSwitchCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StackSwitchCommandSettings settings)
    {
        var console = AnsiConsole.Console;
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new StackSwitchCommandHandler(
            new ConsoleInputProvider(console),
            new GitOperations(outputProvider, settings.GetGitOperationSettings()),
            new StackConfig());

        await handler.Handle(new StackSwitchCommandInputs(settings.Branch));

        return 0;
    }
}

public record StackSwitchCommandInputs(string? Branch);

public record StackSwitchCommandResponse();

public class StackSwitchCommandHandler(
    IInputProvider inputProvider,
    IGitOperations gitOperations,
    IStackConfig stackConfig)
{
    public async Task<StackSwitchCommandResponse> Handle(StackSwitchCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitOperations.GetRemoteUri();
        var currentBranch = gitOperations.GetCurrentBranch();

        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        var branchSelection = inputs.Branch ?? inputProvider.SelectGrouped(
            Questions.SelectBranch,
            stacksForRemote
                .OrderByCurrentStackThenByName(currentBranch)
                .Select(s => new ChoiceGroup<string>(s.Name, [s.SourceBranch, .. s.Branches]))
                .ToArray());

        if (inputs.Branch is not null && !gitOperations.DoesLocalBranchExist(branchSelection))
            throw new InvalidOperationException($"Branch '{branchSelection}' does not exist.");

        gitOperations.ChangeBranch(branchSelection);

        return new();
    }
}