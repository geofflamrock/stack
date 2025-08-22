using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using MoreLinq;
using Spectre.Console;
using Stack.Commands.Helpers;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands;

public class CreatePullRequestsCommand : Command
{
    private readonly CreatePullRequestsCommandHandler handler;

    public CreatePullRequestsCommand(IServiceProvider serviceProvider, CreatePullRequestsCommandHandler handler) 
        : base("create", "Create pull requests for a stack.", serviceProvider)
    {
        this.handler = handler;
        Add(CommonOptions.Stack);
    }

    protected override async Task Execute(ParseResult parseResult, CancellationToken cancellationToken)
    {
        await handler.Handle(new CreatePullRequestsCommandInputs(
            parseResult.GetValue(CommonOptions.Stack)));
    }
}

public record CreatePullRequestsCommandInputs(string? Stack)
{
    public static CreatePullRequestsCommandInputs Empty => new((string?)null);
}

public class CreatePullRequestsCommandHandler(
    IInputProvider inputProvider,
    ILogger logger,
    IGitClient gitClient,
    IGitHubClient gitHubClient,
    IFileOperations fileOperations,
    IStackConfig stackConfig)
    : CommandHandlerBase<CreatePullRequestsCommandInputs>
{
    public override async Task Handle(CreatePullRequestsCommandInputs inputs)
    {
        await Task.CompletedTask;

        var stackData = stackConfig.Load();

        var remoteUri = gitClient.GetRemoteUri();

        var stacksForRemote = stackData.Stacks.Where(s => s.RemoteUri.Equals(remoteUri, StringComparison.OrdinalIgnoreCase)).ToList();

        if (stacksForRemote.Count == 0)
        {
            logger.Information("No stacks found for current repository.");
            return;
        }

        var currentBranch = gitClient.GetCurrentBranch();
        var stack = inputProvider.SelectStack(logger, inputs.Stack, stacksForRemote, currentBranch);

        if (stack is null)
        {
            throw new InvalidOperationException($"Stack '{inputs.Stack}' not found.");
        }

        var status = StackHelpers.GetStackStatus(
            stack,
            currentBranch,
            logger,
            gitClient,
            gitHubClient);

        var pullRequestCreateActions = new List<PullRequestCreateAction>();

        var allBranchLines = status.GetAllBranchLines();

        foreach (var branchLine in allBranchLines)
        {
            var sourceBranch = stack.SourceBranch;

            foreach (var branch in branchLine)
            {
                if (branch.IsActive)
                {
                    if (!branch.HasPullRequest)
                    {
                        pullRequestCreateActions.Add(new PullRequestCreateAction(branch.Name, sourceBranch));
                    }

                    sourceBranch = branch.Name;
                }
            }
        }

        StackHelpers.OutputStackStatus(status, logger);

        logger.NewLine();

        if (pullRequestCreateActions.Count > 0)
        {
            var selectedPullRequestActions = inputProvider.MultiSelect(
                Questions.SelectPullRequestsToCreate,
                pullRequestCreateActions.ToArray(),
                true,
                action => $"{action.Branch} -> {action.BaseBranch}")
                .ToList();

            logger.Information("Select branches to create pull requests for:");

            foreach (var action in selectedPullRequestActions)
            {
                logger.Information($"  {action.Branch} -> {action.BaseBranch}");
            }

            logger.NewLine();

            var pullRequestInformation = GetPullRequestInformation(
                inputProvider,
                logger,
                gitClient,
                fileOperations,
                selectedPullRequestActions);

            logger.NewLine();

            OutputUpdatedStackStatus(logger, stack, status, pullRequestInformation);

            logger.NewLine();

            if (inputProvider.Confirm(Questions.ConfirmCreatePullRequests))
            {
                var newPullRequests = CreatePullRequests(logger, gitHubClient, status, pullRequestInformation);

                var pullRequestsInStack = status.GetAllBranches()
                    .Where(branch => branch.HasPullRequest)
                    .Select(branch => branch.PullRequest!)
                    .Concat(newPullRequests)
                    .ToList();

                if (pullRequestsInStack.Count > 1)
                {
                    StackHelpers.UpdateStackPullRequestList(logger, gitHubClient, stack, pullRequestsInStack);
                }

                if (inputProvider.Confirm(Questions.OpenPullRequests))
                {
                    foreach (var pullRequest in newPullRequests)
                    {
                        gitHubClient.OpenPullRequest(pullRequest);
                    }
                }
            }
        }
        else
        {
            logger.Information("No new pull requests to create.");
        }
    }

    private static List<GitHubPullRequest> CreatePullRequests(
        ILogger logger,
        IGitHubClient gitHubClient,
        StackStatus status,
        List<PullRequestInformation> pullRequestCreateActions)
    {
        var pullRequests = new List<GitHubPullRequest>();
        foreach (var action in pullRequestCreateActions)
        {
            logger.Information($"Creating pull request for branch {action.HeadBranch.Branch()} to {action.BaseBranch.Branch()}");
            var pullRequest = gitHubClient.CreatePullRequest(
                action.HeadBranch,
                action.BaseBranch,
                action.Title!,
                action.BodyFilePath!,
                action.Draft);

            if (pullRequest is not null)
            {
                logger.Information($"Pull request {pullRequest.GetPullRequestDisplay()} created for branch {action.HeadBranch.Branch()} to {action.BaseBranch.Branch()}");
                pullRequests.Add(pullRequest);
            }
        }

        return pullRequests;
    }

    private static void OutputUpdatedStackStatus(
        ILogger logger,
        Config.Stack stack,
        StackStatus status,
        List<PullRequestInformation> pullRequestInformation)
    {
        StackHelpers.OutputStackStatus(
            status,
            logger,
            (branch) =>
            {
                var pr = pullRequestInformation.FirstOrDefault(pr => pr.HeadBranch == branch.Name);

                if (pr is not null)
                {
                    return $" {$"*NEW* {pr.Title}".Highlighted()}{(pr.Draft == true ? " (draft)".Muted() : string.Empty)}";
                }

                return null;
            });
    }

    private static List<PullRequestInformation> GetPullRequestInformation(
        IInputProvider inputProvider,
        ILogger logger,
        IGitClient gitClient,
        IFileOperations fileOperations,
        List<PullRequestCreateAction> pullRequestCreateActions)
    {
        var pullRequestActions = new List<PullRequestInformation>();
        var pullRequestTemplateFileNames = new List<string>(["PULL_REQUEST_TEMPLATE.md", "pull_request_template.md"]);

        var pullRequestTemplatePath = pullRequestTemplateFileNames
            .Select(fileName => Path.Join(gitClient.GetRootOfRepository(), ".github", fileName))
            .FirstOrDefault(fileOperations.Exists);

        if (pullRequestTemplatePath is not null)
        {
            logger.Information($"Found pull request template in repository, this will be used as the default body for each pull request.");
        }

        foreach (var action in pullRequestCreateActions)
        {
            logger.NewLine();
            var pullRequestHeader = $"New pull request from {action.Branch.Branch()} -> {action.BaseBranch.Branch()}";
            logger.Rule(pullRequestHeader);

            var title = inputProvider.Text(Questions.PullRequestTitle);
            var bodyFilePath = Path.Join(fileOperations.GetTempPath(), $"stack-pr-{Guid.NewGuid():N}.md");

            fileOperations.Create(bodyFilePath);

            if (pullRequestTemplatePath is not null)
                fileOperations.Copy(pullRequestTemplatePath, bodyFilePath, true);

            // Add the stack pr list markers to the body
            fileOperations.InsertText(bodyFilePath, 0, $@"{StackConstants.StackMarkerStart}
            
{StackConstants.StackMarkerDescription}

{StackConstants.StackMarkerEnd}");

            var draft = inputProvider.Confirm(Questions.CreatePullRequestAsDraft, false);

            pullRequestActions.Add(new PullRequestInformation(
                action.Branch,
                action.BaseBranch,
                title,
                bodyFilePath,
                draft));
        }

        return pullRequestActions;
    }

    public record PullRequestInformation(
        string HeadBranch,
        string BaseBranch,
        string Title,
        string BodyFilePath,
        bool Draft);

    public record PullRequestCreateAction(string Branch, string BaseBranch);
}
