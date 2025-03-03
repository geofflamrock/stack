using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class PullStackCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to pull changes from the remote for.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }
}

public class PullStackCommand : CommandBase<PullStackCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, PullStackCommandSettings settings)
    {
        var handler = new PullStackCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new StackConfig());

        await handler.Handle(new PullStackCommandInputs(settings.Stack));

        return 0;
    }
}

public record PullStackCommandInputs(string? Stack);
public record PullStackCommandResponse();
public class PullStackCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<PullStackCommandInputs, PullStackCommandResponse>
{
    public override async Task<PullStackCommandResponse> Handle(PullStackCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            logger.Information("No stacks found for current repository.");
            return new();
        }

        var currentBranch = gitClient.GetCurrentBranch();

        var stack = inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");

        StackHelpers.PullChanges(stack, gitClient, logger);

        gitClient.ChangeBranch(currentBranch);
        return new();
    }
}
