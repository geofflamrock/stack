using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MoreLinq.Extensions;
using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using Stack.Infrastructure.Settings;

namespace Stack.Commands;

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
    StackStatusCommandJsonOutputParentBranchStatus? Parent,
    List<StackStatusCommandJsonOutputBranchDetail> Children
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
    string Name,
    int Ahead,
    int Behind
);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<StackStatusCommandJsonOutput>))]
[JsonSerializable(typeof(StackStatusCommandJsonOutput))]
[JsonSerializable(typeof(StackStatusCommandJsonOutputBranch))]
[JsonSerializable(typeof(StackStatusCommandJsonOutputBranchDetail))]
[JsonSerializable(typeof(StackStatusCommandJsonOutputRemoteTrackingBranchStatus))]
[JsonSerializable(typeof(StackStatusCommandJsonOutputCommit))]
[JsonSerializable(typeof(StackStatusCommandJsonOutputGitHubPullRequest))]
[JsonSerializable(typeof(StackStatusCommandJsonOutputParentBranchStatus))]
internal partial class StackStatusCommandJsonSerializerContext : JsonSerializerContext
{
}

public class StackStatusCommand : CommandWithOutput<StackStatusCommandResponse>
{
    static readonly Option<bool> All = new("--all")
    {
        Description = "Show status of all stacks."
    };

    static readonly Option<bool> Full = new("--full")
    {
        Description = "Show full status including pull requests."
    };

    private readonly StackStatusCommandHandler handler;

    public StackStatusCommand(
        ILogger<StackStatusCommand> logger,
        IAnsiConsoleWriter console,
        IInputProvider inputProvider,
        CliExecutionContext executionContext,
        StackStatusCommandHandler handler)
        : base("status", "Show the status of the current stack or all stacks in the repository.", logger, console, inputProvider, executionContext)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
        Add(All);
        Add(Full);
    }

    protected override async Task<StackStatusCommandResponse> ExecuteAndReturnResponse(ParseResult parseResult, CancellationToken cancellationToken)
    {
        return await handler.Handle(
            new StackStatusCommandInputs(
                parseResult.GetValue(CommonOptions.Stack),
                parseResult.GetValue(All),
                parseResult.GetValue(Full)),
            cancellationToken);
    }

    protected override void WriteDefaultOutput(StackStatusCommandResponse response)
    {
        StackHelpers.OutputStackStatus(response.Stacks, Logger, Console);

        if (response.Stacks.Count == 1)
        {
            var stack = response.Stacks.First();
            StackHelpers.OutputBranchAndStackActions(stack, Logger);
        }
    }

    protected override void WriteJsonOutput(StackStatusCommandResponse response)
    {
        var output = response.Stacks.Select(MapToJsonOutput).ToList();
        var json = JsonSerializer.Serialize(output, typeof(List<StackStatusCommandJsonOutput>), StackStatusCommandJsonSerializerContext.Default);
        StdOut.WriteLine(json);
    }

    private static StackStatusCommandJsonOutput MapToJsonOutput(StackStatus stack)
    {
        return new StackStatusCommandJsonOutput(
            stack.Name,
            MapBranch(stack.SourceBranch),
            [.. stack.Branches.Select(MapBranchDetail)]
        );
    }

    private static StackStatusCommandJsonOutputBranch MapBranch(BranchDetailBase branch)
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
        var branchDetail = new StackStatusCommandJsonOutputBranchDetail(
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
                branch.Parent.Name,
                branch.Parent.Ahead,
                branch.Parent.Behind
            ),
            []
        );

        branchDetail.Children.AddRange(branch.Children.Select(MapBranchDetail));
        return branchDetail;
    }
}

public record StackStatusCommandInputs(string? Stack, bool All, bool Full);
public record StackStatusCommandResponse(List<StackStatus> Stacks);

public class StackStatusCommandHandler(
    IInputProvider inputProvider,
    ILogger<StackStatusCommandHandler> logger,
    IAnsiConsoleWriter console,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IStackConfig stackConfig)
    : CommandHandlerBase<StackStatusCommandInputs, StackStatusCommandResponse>
{
    public override async Task<StackStatusCommandResponse> Handle(StackStatusCommandInputs inputs, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();
        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();
        var currentBranch = gitClient.GetCurrentBranch();

        var stacksToCheckStatusFor = new List<Config.Stack>();

        if (inputs.All)
        {
            stacksToCheckStatusFor.AddRange(stacksForRemote);
        }
        else
        {
            var stack = await inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch, cancellationToken);

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
            console,
            gitClient,
            gitHubClient,
            inputs.Full);

        return new StackStatusCommandResponse(stackStatusResults);
    }
}