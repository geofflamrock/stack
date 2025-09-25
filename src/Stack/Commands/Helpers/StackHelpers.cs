using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Spectre.Console;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;
using static Stack.Commands.CreatePullRequestsCommandHandler;

namespace Stack.Commands.Helpers;

public record StackStatus(string Name, SourceBranchDetail SourceBranch, List<BranchDetail> Branches)
{
    public List<List<BranchDetail>> GetAllBranchLines()
    {
        var allLines = new List<List<BranchDetail>>();
        foreach (var branch in Branches)
        {
            allLines.AddRange(branch.GetAllPaths());
        }
        return allLines;
    }

    public List<BranchDetail> GetAllBranches()
    {
        var branchesToReturn = new List<BranchDetail>();
        foreach (var branch in Branches)
        {
            branchesToReturn.Add(branch);
            branchesToReturn.AddRange(GetAllBranches(branch));
        }

        return branchesToReturn;
    }

    static List<BranchDetail> GetAllBranches(BranchDetail branch)
    {
        var branchesToReturn = new List<BranchDetail>();
        foreach (var child in branch.Children)
        {
            branchesToReturn.Add(child);
            branchesToReturn.AddRange(GetAllBranches(child));
        }

        return branchesToReturn;
    }
}

public record RemoteTrackingBranchStatus(string Name, bool Exists, int Ahead, int Behind);

public record BranchDetailBase(string Name, bool Exists, Commit? Tip, RemoteTrackingBranchStatus? RemoteTrackingBranch)
{
    public virtual bool IsActive => Exists && RemoteTrackingBranch?.Exists == true;
    public int AheadOfRemote => RemoteTrackingBranch?.Ahead ?? 0;
    public int BehindRemote => RemoteTrackingBranch?.Behind ?? 0;
}

public record SourceBranchDetail(string Name, bool Exists, Commit? Tip, RemoteTrackingBranchStatus? RemoteTrackingBranch) : BranchDetailBase(Name, Exists, Tip, RemoteTrackingBranch);

public record BranchDetail(
    string Name,
    bool Exists,
    Commit? Tip,
    RemoteTrackingBranchStatus? RemoteTrackingBranch,
    GitHubPullRequest? PullRequest,
    ParentBranchStatus? Parent,
    List<BranchDetail> Children) : BranchDetailBase(Name, Exists, Tip, RemoteTrackingBranch)
{
    public override bool IsActive => base.IsActive && (PullRequest is null || PullRequest.State != GitHubPullRequestStates.Merged);
    public bool CouldBeCleanedUp => Exists && ((RemoteTrackingBranch is not null && !RemoteTrackingBranch.Exists) || (PullRequest is not null && PullRequest.State == GitHubPullRequestStates.Merged));
    public bool HasPullRequest => PullRequest is not null && PullRequest.State != GitHubPullRequestStates.Closed;
    public int AheadOfParent => Parent?.Ahead ?? 0;
    public int BehindParent => Parent?.Behind ?? 0;
    public string ParentBranchName => Parent?.Name ?? string.Empty;

    public List<List<BranchDetail>> GetAllPaths()
    {
        var result = new List<List<BranchDetail>>();
        if (Children.Count == 0)
        {
            result.Add([this]);
        }
        else
        {
            foreach (var child in Children)
            {
                foreach (var path in child.GetAllPaths())
                {
                    var newPath = new List<BranchDetail> { this };
                    newPath.AddRange(path);
                    result.Add(newPath);
                }
            }
        }
        return result;
    }
}

public record ParentBranchStatus(string Name, int Ahead, int Behind);

public static class StackHelpers
{
    public static List<StackStatus> GetStackStatus(
        List<Config.Stack> stacks,
        string currentBranch,
        ILogger logger,
        IGitClient gitClient,
        IGitHubClient gitHubClient,
        bool includePullRequestStatus)
    {
        var stacksToReturnStatusFor = new List<StackStatus>();

        var stacksOrderedByCurrentBranch = stacks
            .OrderByCurrentStackThenByName(currentBranch);

        var allBranchesInStacks = stacks
            .SelectMany(s => (new[] { s.SourceBranch }).Concat(s.AllBranchNames))
            .Distinct()
            .ToArray();

        var branchStatuses = gitClient.GetBranchStatuses(allBranchesInStacks);
        EvaluateBranchStatusDetails(logger, gitClient, gitHubClient, includePullRequestStatus, stacksToReturnStatusFor, stacksOrderedByCurrentBranch, branchStatuses);

        return stacksToReturnStatusFor;

        static void EvaluateBranchStatusDetails(ILogger logger, IGitClient gitClient, IGitHubClient gitHubClient, bool includePullRequestStatus, List<StackStatus> stacksToReturnStatusFor, IOrderedEnumerable<Config.Stack> stacksOrderedByCurrentBranch, Dictionary<string, GitBranchStatus> branchStatuses)
        {
            foreach (var stack in stacksOrderedByCurrentBranch)
            {
                if (!branchStatuses.TryGetValue(stack.SourceBranch, out var sourceBranchStatus))
                {
                    logger.SourceBranchDoesNotExist(stack.SourceBranch);
                    continue;
                }

                var sourceBranch = new SourceBranchDetail(
                    stack.SourceBranch,
                    true,
                    sourceBranchStatus.Tip,
                    sourceBranchStatus.RemoteTrackingBranchName is not null
                        ? new RemoteTrackingBranchStatus(
                            sourceBranchStatus.RemoteTrackingBranchName,
                            sourceBranchStatus.RemoteBranchExists,
                            sourceBranchStatus.Ahead,
                            sourceBranchStatus.Behind)
                        : null);
                var stackBranches = new List<BranchDetail>();

                foreach (var branch in stack.Branches)
                {
                    var branchDetail = AddBranchDetailsForAllChildren(gitClient, gitHubClient, includePullRequestStatus, branchStatuses, sourceBranch, branch);
                    stackBranches.Add(branchDetail);
                }

                stacksToReturnStatusFor.Add(new StackStatus(stack.Name, sourceBranch, [.. stackBranches]));
            }

            static BranchDetail AddBranchDetailsForAllChildren(IGitClient gitClient, IGitHubClient gitHubClient, bool includePullRequestStatus, Dictionary<string, GitBranchStatus> branchStatuses, BranchDetailBase parentBranch, Branch branch)
            {
                var branchDetail = CreateBranchDetail(gitClient, gitHubClient, includePullRequestStatus, branchStatuses, parentBranch, branch);

                foreach (var childBranch in branch.Children)
                {
                    var childBranchDetails = AddBranchDetailsForAllChildren(gitClient, gitHubClient, includePullRequestStatus, branchStatuses, branchDetail.IsActive ? branchDetail : parentBranch, childBranch);
                    branchDetail.Children.Add(childBranchDetails);
                }

                return branchDetail;
            }

            static BranchDetail CreateBranchDetail(IGitClient gitClient, IGitHubClient gitHubClient, bool includePullRequestStatus, Dictionary<string, GitBranchStatus> branchStatuses, BranchDetailBase parentBranch, Branch branch)
            {
                branchStatuses.TryGetValue(branch.Name, out var branchStatus);

                if (branchStatus is not null)
                {
                    var (aheadOfParent, behindParent) = branchStatus.RemoteBranchExists ? gitClient.CompareBranches(branch.Name, parentBranch.Name) : (0, 0);
                    GitHubPullRequest? pullRequest = null;

                    if (includePullRequestStatus)
                    {
                        pullRequest = gitHubClient.GetPullRequest(branch.Name);
                    }

                    return new BranchDetail(
                        branch.Name,
                        true,
                        branchStatus.Tip,
                        branchStatus.RemoteTrackingBranchName is not null
                            ? new RemoteTrackingBranchStatus(
                                branchStatus.RemoteTrackingBranchName,
                                branchStatus.RemoteBranchExists,
                                branchStatus.Ahead,
                                branchStatus.Behind)
                            : null,
                        pullRequest,
                        new ParentBranchStatus(parentBranch.Name, aheadOfParent, behindParent),
                        []);
                }
                else
                {
                    GitHubPullRequest? pullRequest = null;

                    if (includePullRequestStatus)
                    {
                        pullRequest = gitHubClient.GetPullRequest(branch.Name);
                    }

                    return new BranchDetail(branch.Name, false, null, null, pullRequest, null, []);
                }
            }
        }
    }

    public static StackStatus GetStackStatus(
        Config.Stack stack,
        string currentBranch,
        ILogger logger,
        IGitClient gitClient,
        IGitHubClient gitHubClient,
        bool includePullRequestStatus)
    {
        var statuses = GetStackStatus(
            [stack],
            currentBranch,
            logger,
            gitClient,
            gitHubClient,
            includePullRequestStatus);

        return statuses.First();
    }

    public static async Task OutputStackStatus(
        List<StackStatus> statuses,
        IOutputProvider outputProvider,
        CancellationToken cancellationToken)
    {
        foreach (var status in statuses)
        {
            await OutputStackStatus(status, outputProvider, cancellationToken);
        }
    }

    public static async Task OutputStackStatus(
        StackStatus status,
        IOutputProvider outputProvider,
        CancellationToken cancellationToken,
        Func<BranchDetail, string?>? getBranchPullRequestDisplay = null)
    {
        var header = GetBranchStatusOutput(status.SourceBranch);
        var items = new List<TreeItem<string>>();

        foreach (var branch in status.Branches)
        {
            items.Add(GetBranchAndPullRequestStatusOutput(branch, getBranchPullRequestDisplay));
        }

        var tree = RenderingHelpers.RenderTree(header, items);

        await outputProvider.WriteMessage(status.Name.Stack(), cancellationToken);
        await outputProvider.WriteLine(tree, cancellationToken);
    }

    public static TreeItem<string> GetBranchAndPullRequestStatusOutput(
        BranchDetail branch,
        Func<BranchDetail, string?>? getBranchPullRequestDisplay = null)
    {
        var branchNameBuilder = new StringBuilder();
        branchNameBuilder.Append(GetBranchStatusOutput(branch));

        var pullRequestDisplay = getBranchPullRequestDisplay?.Invoke(branch);

        if (pullRequestDisplay is not null)
        {
            branchNameBuilder.Append($"   {pullRequestDisplay}");
        }
        else if (branch.PullRequest is not null)
        {
            branchNameBuilder.Append($"   {branch.PullRequest.GetPullRequestDisplay()}");
        }

        var treeItemValue = branchNameBuilder.ToString();
        var children = branch.Children
            .Select(b => GetBranchAndPullRequestStatusOutput(b, getBranchPullRequestDisplay))
            .ToList();

        return new TreeItem<string>(treeItemValue, children);
    }

    public static TreeItem<string> GetBranchAndPullRequestInformation(BranchDetail branch, List<PullRequestInformation> pullRequestInformation)
    {
        var branchNameBuilder = new StringBuilder();
        branchNameBuilder.Append(GetBranchStatusOutput(branch));
        var prInformationForBranch = pullRequestInformation.FirstOrDefault(a => a.HeadBranch == branch.Name);

        if (prInformationForBranch is not null)
        {
            branchNameBuilder.Append($" {$"*NEW* {prInformationForBranch.Title}".Highlighted()}{(prInformationForBranch.Draft == true ? " (draft)".Muted() : string.Empty)}");
        }

        var treeItemValue = branchNameBuilder.ToString();
        var children = branch.Children
            .Select(b => GetBranchAndPullRequestInformation(b, pullRequestInformation))
            .ToList();

        return new TreeItem<string>(treeItemValue, children);
    }

    public static string GetBranchStatusOutput(BranchDetailBase branch)
    {
        var branchNameBuilder = new StringBuilder();

        var branchName = branch.Name;
        Color? color = branch.Exists ? null : Color.Grey;
        Decoration? decoration = branch.Exists ? null : Decoration.Strikethrough;

        if (color is not null && decoration is not null)
        {
            branchNameBuilder.Append($"[{decoration} {color}]{branchName}[/]");
        }
        else if (color is not null)
        {
            branchNameBuilder.Append($"[{color}]{branchName}[/]");
        }
        else if (decoration is not null)
        {
            branchNameBuilder.Append($"[{decoration}]{branchName}[/]");
        }
        else
        {
            branchNameBuilder.Append(branchName);
        }

        if (branch.IsActive)
        {
            if (branch.AheadOfRemote > 0 || branch.BehindRemote > 0)
            {
                branchNameBuilder.Append($" {branch.BehindRemote}{Emoji.Known.DownArrow}{branch.AheadOfRemote}{Emoji.Known.UpArrow}");
            }
        }
        else if (branch.Exists && branch.RemoteTrackingBranch is null)
        {
            branchNameBuilder.Append(" (no remote tracking branch)".Muted());
        }
        else if (branch.Exists && branch.RemoteTrackingBranch is not null && branch.RemoteTrackingBranch.Exists == false)
        {
            branchNameBuilder.Append(" (remote branch deleted)".Muted());
        }

        if (branch.Exists && branch.Tip is not null)
        {
            branchNameBuilder.Append($"   {branch.Tip.Sha[..7]} {Markup.Escape(branch.Tip.Message)}");
        }

        return branchNameBuilder.ToString();
    }

    public static string GetBranchStatusOutput(BranchDetail branch)
    {
        var branchNameBuilder = new StringBuilder();

        var branchName = branch.Name;
        Color? color = branch.Exists ? null : Color.Grey;
        Decoration? decoration = branch.Exists ? null : Decoration.Strikethrough;

        if (color is not null && decoration is not null)
        {
            branchNameBuilder.Append($"[{decoration} {color}]{branchName}[/]");
        }
        else if (color is not null)
        {
            branchNameBuilder.Append($"[{color}]{branchName}[/]");
        }
        else if (decoration is not null)
        {
            branchNameBuilder.Append($"[{decoration}]{branchName}[/]");
        }
        else
        {
            branchNameBuilder.Append(branchName);
        }

        if (branch.IsActive)
        {
            if (branch.AheadOfRemote > 0 || branch.BehindRemote > 0)
            {
                branchNameBuilder.Append($" {branch.BehindRemote}{Emoji.Known.DownArrow}{branch.AheadOfRemote}{Emoji.Known.UpArrow}");
            }

            if (branch.AheadOfParent > 0 && branch.BehindParent > 0)
            {
                branchNameBuilder.Append($" ({branch.AheadOfParent} ahead, {branch.BehindParent} behind {branch.ParentBranchName})".Muted());
            }
            else if (branch.AheadOfParent > 0)
            {
                branchNameBuilder.Append($" ({branch.AheadOfParent} ahead of {branch.ParentBranchName})".Muted());
            }
            else if (branch.BehindParent > 0)
            {
                branchNameBuilder.Append($" ({branch.BehindParent} behind {branch.ParentBranchName})".Muted());
            }
        }
        else if (branch.Exists && branch.RemoteTrackingBranch is null)
        {
            branchNameBuilder.Append(" (no remote tracking branch)".Muted());
        }
        else if (branch.Exists && branch.RemoteTrackingBranch is not null && branch.RemoteTrackingBranch.Exists == false)
        {
            branchNameBuilder.Append(" (remote branch deleted)".Muted());
        }
        else if (branch.PullRequest is not null && branch.PullRequest.State == GitHubPullRequestStates.Merged)
        {
            branchNameBuilder.Append(" (pull request merged)".Muted());
        }

        if (branch.Tip is not null)
        {
            branchNameBuilder.Append($"   {branch.Tip.Sha[..7]} {Markup.Escape(branch.Tip.Message)}");
        }

        return branchNameBuilder.ToString();
    }

    public static async Task OutputBranchAndStackActions(
        StackStatus status,
        IOutputProvider outputProvider,
        CancellationToken cancellationToken)
    {
        var allBranches = status.GetAllBranches();
        if (allBranches.All(branch => branch.CouldBeCleanedUp))
        {
            await outputProvider.WriteMessage("All branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open. This stack might be able to be deleted.", cancellationToken);
            await outputProvider.WriteNewLine(cancellationToken);
            await outputProvider.WriteMessage($"Run {$"stack delete --stack \"{status.Name}\"".Example()} to delete the stack if it's no longer needed.", cancellationToken);
            await outputProvider.WriteNewLine(cancellationToken);
        }
        else if (allBranches.Any(branch => branch.CouldBeCleanedUp))
        {
            await outputProvider.WriteMessage("Some branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open.", cancellationToken);
            await outputProvider.WriteNewLine(cancellationToken);
            await outputProvider.WriteMessage($"Run {$"stack cleanup --stack \"{status.Name}\"".Example()} to clean up the stack if it's no longer needed.", cancellationToken);
            await outputProvider.WriteNewLine(cancellationToken);
        }
        else if (allBranches.All(branch => !branch.Exists))
        {
            await outputProvider.WriteMessage("No branches exist locally. This stack might be able to be deleted.", cancellationToken);
            await outputProvider.WriteNewLine(cancellationToken);
            await outputProvider.WriteMessage($"Run {$"stack delete --stack \"{status.Name}\"".Example()} to delete the stack if it's no longer needed.", cancellationToken);
            await outputProvider.WriteNewLine(cancellationToken);
        }

        if (allBranches.Any(branch => branch.Exists && (branch.RemoteTrackingBranch is null || branch.RemoteTrackingBranch.Ahead > 0)))
        {
            await outputProvider.WriteMessage("There are changes in local branches that have not been pushed to the remote repository.", cancellationToken);
            await outputProvider.WriteNewLine(cancellationToken);
            await outputProvider.WriteMessage($"Run {$"stack push --stack \"{status.Name}\"".Example()} to push the changes to the remote repository.", cancellationToken);
            await outputProvider.WriteNewLine(cancellationToken);
        }

        if (allBranches.Any(branch => branch.Exists && branch.RemoteTrackingBranch is not null && branch.RemoteTrackingBranch.Behind > 0))
        {
            await outputProvider.WriteMessage("There are changes in source branches that have not been applied to the stack.", cancellationToken);
            await outputProvider.WriteNewLine(cancellationToken);
            await outputProvider.WriteMessage($"Run {$"stack update --stack \"{status.Name}\"".Example()} to update the stack locally.", cancellationToken);
            await outputProvider.WriteNewLine(cancellationToken);
            await outputProvider.WriteMessage($"Run {$"stack sync --stack \"{status.Name}\"".Example()} to sync the stack with the remote repository.", cancellationToken);
            await outputProvider.WriteNewLine(cancellationToken);
        }
    }

    public static UpdateStrategy? GetUpdateStrategyConfigValue(IGitClient gitClient)
    {
        var strategyConfigValue = gitClient.GetConfigValue("stack.update.strategy");

        if (strategyConfigValue is not null)
        {
            if (Enum.TryParse<UpdateStrategy>(strategyConfigValue, true, out var configuredStrategy))
            {
                return configuredStrategy;
            }
            else
            {
                throw new InvalidOperationException($"Invalid value '{strategyConfigValue}' for 'stack.update.strategy'.");
            }
        }

        return null;
    }

    public static async Task<UpdateStrategy> GetUpdateStrategy(
        UpdateStrategy? specificUpdateStrategy,
        IGitClient gitClient,
        IInputProvider inputProvider,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (specificUpdateStrategy is not null)
        {
            return specificUpdateStrategy.Value;
        }

        var strategyFromConfig = GetUpdateStrategyConfigValue(gitClient);

        if (strategyFromConfig is not null)
        {
            return strategyFromConfig.Value;
        }

        var strategy = await inputProvider.Select(
            Questions.SelectUpdateStrategy,
            [UpdateStrategy.Merge, UpdateStrategy.Rebase],
            cancellationToken);

        logger.Answer(Questions.SelectUpdateStrategy, strategy);

        return strategy;
    }

    public static string[] GetBranchesNeedingCleanup(Config.Stack stack, ILogger logger, IGitClient gitClient, IGitHubClient gitHubClient)
    {
        var currentBranch = gitClient.GetCurrentBranch();
        var stackStatus = GetStackStatus(stack, currentBranch, logger, gitClient, gitHubClient, false);

        return [.. stackStatus.GetAllBranches().Where(b => b.CouldBeCleanedUp).Select(b => b.Name)];
    }

    public static void OutputBranchesNeedingCleanup(ILogger logger, string[] branches)
    {
        logger.BranchesToCleanupHeader();

        foreach (var branch in branches)
        {
            logger.BranchToCleanup(branch);
        }
    }

    public static void CleanupBranches(IGitClient gitClient, ILogger logger, string[] branches)
    {
        foreach (var branch in branches)
        {
            logger.DeletingLocalBranch(branch);
            gitClient.DeleteLocalBranch(branch);
        }
    }

    public static void UpdateStackPullRequestList(
        ILogger logger,
        IGitHubClient gitHubClient,
        Config.Stack stack,
        List<GitHubPullRequest> pullRequestsInStack)
    {
        var prListBuilder = new StringBuilder();

        void AppendPullRequestToList(GitHubPullRequest pullRequest, int indentLevel)
        {
            prListBuilder.AppendLine($"{new string(' ', indentLevel * 2)}- {pullRequest.Url}");
        }

        void AppendBranchPullRequestsToList(Branch branch, int indentLevel)
        {
            var pullRequest = pullRequestsInStack.FirstOrDefault(pr => pr.HeadRefName == branch.Name);
            if (pullRequest is not null)
            {
                AppendPullRequestToList(pullRequest, indentLevel);
            }

            foreach (var child in branch.Children)
            {
                AppendBranchPullRequestsToList(child, indentLevel + 1);
            }
        }

        foreach (var branch in stack.Branches)
        {
            AppendBranchPullRequestsToList(branch, 0);
        }

        // Edit each PR and add to the top of the description
        // the details of each PR in the stack
        var prBodyMarkdown = $"{StackConstants.StackMarkerStart}{Environment.NewLine}{prListBuilder}{StackConstants.StackMarkerEnd}";

        foreach (var pullRequest in pullRequestsInStack)
        {
            // Find the existing part of the PR body that has the PR list
            // and replace it with the updated PR list
            var prBody = pullRequest.Body;

            var prListStart = prBody.IndexOf(StackConstants.StackMarkerStart, StringComparison.OrdinalIgnoreCase);
            var prListEnd = prBody.IndexOf(StackConstants.StackMarkerEnd, StringComparison.OrdinalIgnoreCase);

            if (prListStart >= 0 && prListEnd >= 0)
            {
                prBody = prBody.Remove(prListStart, prListEnd - prListStart + StackConstants.StackMarkerEnd.Length);
                prBody = prBody.Insert(prListStart, prBodyMarkdown);

                logger.UpdatingPullRequestWithStackDetails(pullRequest.GetPullRequestDisplay());

                gitHubClient.EditPullRequest(pullRequest, prBody);
            }
        }
    }
}

public enum MergeConflictAction
{
    Abort,
    Continue
}

public enum UpdateStrategy
{
    Merge,
    Rebase
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Source branch {SourceBranch} does not exist locally or in the remote repository.")]
    public static partial void SourceBranchDoesNotExist(this ILogger logger, string sourceBranch);

    [LoggerMessage(Level = LogLevel.Information, Message = "The following branches exist locally but are either not in the remote repository or the pull request associated with the branch is no longer open:")]
    public static partial void BranchesToCleanupHeader(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "  {Branch}")]
    public static partial void BranchToCleanup(this ILogger logger, string branch);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deleting local branch {Branch}")]
    public static partial void DeletingLocalBranch(this ILogger logger, string branch);

    [LoggerMessage(Level = LogLevel.Information, Message = "Updating pull request {PullRequest} with stack details")]
    public static partial void UpdatingPullRequestWithStackDetails(this ILogger logger, string pullRequest);
}