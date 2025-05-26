using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class StackStatusCommandSettings : CommandWithOutputSettingsBase
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
}

public record StackStatusCommandJsonOutput
(
    string Name,
    StackStatusCommandJsonOutputBranch SourceBranch,
    List<StackStatusCommandJsonOutputBranchDetail> Branches
);

public record StackStatusCommandJsonOutputBranch
(
    string Name,
    bool Exists,
    StackStatusCommandJsonOutputCommit? Tip,
    StackStatusCommandJsonOutputRemoteTrackingBranchStatus? RemoteTrackingBranch
);

public record StackStatusCommandJsonOutputBranchDetail
(
    string Name,
    bool Exists,
    StackStatusCommandJsonOutputCommit? Tip,
    StackStatusCommandJsonOutputRemoteTrackingBranchStatus? RemoteTrackingBranch,
    StackStatusCommandJsonOutputGitHubPullRequest? PullRequest,
    StackStatusCommandJsonOutputParentBranchStatus? Parent
) : StackStatusCommandJsonOutputBranch(Name, Exists, Tip, RemoteTrackingBranch);

public record StackStatusCommandJsonOutputRemoteTrackingBranchStatus
(
    string Name,
    bool Exists,
    int Ahead,
    int Behind
);

public record StackStatusCommandJsonOutputCommit
(
    string Sha,
    string Message
);

public record StackStatusCommandJsonOutputGitHubPullRequest
(
    int Number,
    string Title,
    string State,
    Uri Url,
    bool IsDraft
);

public record StackStatusCommandJsonOutputParentBranchStatus
(
    StackStatusCommandJsonOutputBranch Branch,
    int Ahead,
    int Behind
);

public class StackStatusCommand : CommandWithOutput<StackStatusCommandSettings, StackStatusCommandResponse>
{
    protected override async Task<StackStatusCommandResponse> Execute(StackStatusCommandSettings settings)
    {
        var handler = new StackStatusCommandHandler(
            InputProvider,
            StdErrLogger,
            new GitClient(StdErrLogger, settings.GetGitClientSettings()),
            new GitHubClient(StdErrLogger, settings.GetGitHubClientSettings()),
            new StackConfig());

        return await handler.Handle(new StackStatusCommandInputs(settings.Stack, settings.All, settings.Full));
    }

    protected override void WriteDefaultOutput(StackStatusCommandResponse response)
    {
        StackHelpers.OutputStackStatus(response.Stacks, StdOutLogger);

        if (response.Stacks.Count == 1)
        {
            var stack = response.Stacks.First();
            StackHelpers.OutputBranchAndStackActions(stack, StdOutLogger);
        }
    }

    protected override void WriteJsonOutput(StackStatusCommandResponse response, JsonSerializerOptions options)
    {
        var output = response.Stacks.Select(MapToJsonOutput).ToList();
        var json = JsonSerializer.Serialize(output, options);
        StdOut.WriteLine(json);
    }

    private static StackStatusCommandJsonOutput MapToJsonOutput(StackStatus stack)
    {
        return new StackStatusCommandJsonOutput(
            stack.Name,
            MapBranch(stack.SourceBranch),
            stack.Branches.Select(MapBranchDetail).ToList()
        );
    }

    private static StackStatusCommandJsonOutputBranch MapBranch(Branch branch)
    {
        return new StackStatusCommandJsonOutputBranch(
            branch.Name,
            branch.Exists,
            branch.Tip is null ? null : new StackStatusCommandJsonOutputCommit(branch.Tip.Sha, branch.Tip.Message),
            branch.RemoteTrackingBranch is null ? null : new StackStatusCommandJsonOutputRemoteTrackingBranchStatus(
                branch.RemoteTrackingBranch.Name,
                branch.RemoteTrackingBranch.Exists,
                branch.RemoteTrackingBranch.Ahead,
                branch.RemoteTrackingBranch.Behind
            )
        );
    }

    private static StackStatusCommandJsonOutputBranchDetail MapBranchDetail(BranchDetail branch)
    {
        return new StackStatusCommandJsonOutputBranchDetail(
            branch.Name,
            branch.Exists,
            branch.Tip is null ? null : new StackStatusCommandJsonOutputCommit(branch.Tip.Sha, branch.Tip.Message),
            branch.RemoteTrackingBranch is null ? null : new StackStatusCommandJsonOutputRemoteTrackingBranchStatus(
                branch.RemoteTrackingBranch.Name,
                branch.RemoteTrackingBranch.Exists,
                branch.RemoteTrackingBranch.Ahead,
                branch.RemoteTrackingBranch.Behind
            ),
            branch.PullRequest is null ? null : new StackStatusCommandJsonOutputGitHubPullRequest(
                branch.PullRequest.Number,
                branch.PullRequest.Title,
                branch.PullRequest.State,
                branch.PullRequest.Url,
                branch.PullRequest.IsDraft
            ),
            branch.Parent is null ? null : new StackStatusCommandJsonOutputParentBranchStatus(
                MapBranch(branch.Parent.Branch),
                branch.Parent.Ahead,
                branch.Parent.Behind
            )
        );
    }
}

public record StackStatusCommandInputs(string? Stack, bool All, bool Full);
public record StackStatusCommandResponse(List<StackStatus> Stacks);

public class StackStatusCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<StackStatusCommandInputs, StackStatusCommandResponse>
{
    public override async Task<StackStatusCommandResponse> Handle(StackStatusCommandInputs inputs)
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
            var stack = inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch);

            if (stack is null)
            {
                throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
            }

            stacksToCheckStatusFor.Add(stack);
        }

        var stackStatusResults = StackHelpers.GetStackStatus(
            stacksToCheckStatusFor,
            currentBranch,
            logger,
            gitClient,
            gitHubClient,
            inputs.Full);

        return new StackStatusCommandResponse(stackStatusResults);
    }
}