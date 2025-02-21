using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class StackStatusCommandSettings : CommandSettingsBase
{
    [Description("The name of the stack to show the status of.")]
    [CommandOption("-s|--stack")]
    public string? Stack { get; init; }

    [Description("Show status of all stacks.")]
    [CommandOption("--all")]
    public bool All { get; init; }

    [Description("Show full status including pull requests.")]
    [CommandOption("--full")]
    public bool Full { get; init; }

    [Description("Output the status of stacks in JSON format.")]
    [CommandOption("--json")]
    public bool Json { get; init; }
}

public class StackStatusCommand : AsyncCommand<StackStatusCommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, StackStatusCommandSettings settings)
    {
        var console = AnsiConsole.Console;
        var outputProvider = new ConsoleOutputProvider(console);

        var handler = new StackStatusCommandHandler(
            new ConsoleInputProvider(console),
            outputProvider,
            new GitClient(outputProvider, settings.GetGitClientSettings()),
            new GitHubClient(outputProvider, settings.GetGitHubClientSettings()),
            new StackConfig());

        await handler.Handle(new StackStatusCommandInputs(settings.Stack, settings.All, settings.Full, settings.Json));

        return 0;
    }
}

public record StackStatusCommandInputs(string? Stack, bool All, bool Full, bool Json = false);
public record StackStatusCommandResponse(Dictionary<Config.Stack, StackStatus> Statuses);

public class StackStatusCommandHandler(
    IInputProvider inputProvider,
    IOutputProvider outputProvider,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
{
    public async Task<StackStatusCommandResponse> Handle(StackStatusCommandInputs inputs)
    {
        await Task.CompletedTask;
        var stacks = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var stacksForRemote = stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksToCheckStatusFor = new List<Config.Stack>();

        if (inputs.All)
        {
            stacksToCheckStatusFor.AddRange(stacksForRemote);
        }
        else
        {
            var stack = inputProvider.SelectStack(outputProvider, inputs.Stack, stacksForRemote, currentBranch, !inputs.Json);

            if (stack is null)
            {
                throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
            }

            stacksToCheckStatusFor.Add(stack);
        }

        var stackStatusResults = StackHelpers.GetStackStatus(
            stacksToCheckStatusFor,
            currentBranch,
            outputProvider,
            gitClient,
            gitHubClient,
            inputs.Full,
            !inputs.Json);

        if (inputs.Json)
        {
            StackStatusOutput[] stackStatusOutput = [.. stackStatusResults.Select(kvp => new StackStatusOutput(kvp.Key.Name, kvp.Key.SourceBranch, [.. kvp.Key.Branches], kvp.Value))];
            outputProvider.Information(Markup.Escape(JsonSerializer.Serialize(stackStatusOutput, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })));
            return new StackStatusCommandResponse(stackStatusResults);
        }

        if (stackStatusResults.Count == 1)
        {
            outputProvider.NewLine();
        }

        StackHelpers.OutputStackStatus(stackStatusResults, outputProvider);

        if (stacksToCheckStatusFor.Count == 1)
        {
            var (stack, status) = stackStatusResults.First();
            StackHelpers.OutputBranchAndStackActions(stack, status, outputProvider);
        }

        return new StackStatusCommandResponse(stackStatusResults);
    }


    record StackStatusOutput(string Name, string SourceBranch, string[] Branches, StackStatus Status);
}
