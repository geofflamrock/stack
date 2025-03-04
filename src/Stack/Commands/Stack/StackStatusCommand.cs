using System.ComponentModel;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Stack.Commands;
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
        StackHelpers.OutputStackStatus(response.Statuses, StdOutLogger);

        if (response.Statuses.Count == 1)
        {
            var (stack, status) = response.Statuses.First();
            StackHelpers.OutputBranchAndStackActions(stack, status, StdOutLogger);
        }
    }

    protected override void WriteJsonOutput(StackStatusCommandResponse response, JsonSerializerOptions options)
    {
        var stackDetails = new List<StackDetail>();

        foreach (var (stack, status) in response.Statuses)
        {
            status.Branches.TryGetValue(stack.SourceBranch, out var sourceBranchStatus);

            if (sourceBranchStatus is not null)
            {
                var sourceBranch = new Branch(
                    stack.SourceBranch,
                    sourceBranchStatus.Status.ExistsLocally,
                    sourceBranchStatus.Status.Tip,
                    sourceBranchStatus.Status.HasRemoteTrackingBranch ?
                        new RemoteTrackingBranchStatus(
                            $"origin/{stack.SourceBranch}",
                            sourceBranchStatus.Status.ExistsInRemote,
                            sourceBranchStatus.Status.AheadOfRemote,
                            sourceBranchStatus.Status.BehindRemote) : null);

                var branches = new List<BranchDetail>();
                var parentBranch = sourceBranch;

                foreach (var branch in stack.Branches)
                {
                    status.Branches.TryGetValue(branch, out var branchStatus);

                    if (branchStatus is not null)
                    {
                        var pullRequest = branchStatus.PullRequest;
                        var remoteTrackingBranch = branchStatus.Status.HasRemoteTrackingBranch ?
                            new RemoteTrackingBranchStatus(
                                $"origin/{branch}",
                                branchStatus.Status.ExistsInRemote,
                                branchStatus.Status.AheadOfRemote,
                                branchStatus.Status.BehindRemote) : null;

                        var parentBranchStatus = new ParentBranchStatus(parentBranch, branchStatus.Status.AheadOfParent, branchStatus.Status.BehindParent);

                        var branchDetail = new BranchDetail(
                            branch,
                            branchStatus.Status.ExistsLocally,
                            branchStatus.Status.Tip,
                            remoteTrackingBranch,
                            pullRequest,
                            parentBranchStatus);

                        branches.Add(branchDetail);

                        if (branchStatus.IsActive)
                        {
                            parentBranch = branchDetail;
                        }
                    }
                }

                stackDetails.Add(new StackDetail(stack.Name, sourceBranch, [.. branches]));
            }
        }

        var json = JsonSerializer.Serialize(stackDetails, options);
        StdOut.WriteLine(json);
    }
}

record StackDetail(string Name, Branch SourceBranch, BranchDetail[] Branches);

record RemoteTrackingBranchStatus(string Name, bool Exists, int Ahead, int Behind);

record Branch(string Name, bool Exists, Commit? Tip, RemoteTrackingBranchStatus? RemoteTrackingBranch);

record BranchDetail(
    string Name,
    bool Exists,
    Commit? Tip,
    RemoteTrackingBranchStatus? RemoteTrackingBranch,
    GitHubPullRequest? PullRequest,
    ParentBranchStatus? Parent) : Branch(Name, Exists, Tip, RemoteTrackingBranch);

record ParentBranchStatus(Branch Branch, int Ahead, int Behind);

public record StackStatusCommandInputs(string? Stack, bool All, bool Full);
public record StackStatusCommandResponse(Dictionary<Config.Stack, StackStatus> Statuses);

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
