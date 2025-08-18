using MoreLinq;
using Stack.Config;
using Stack.Git;
using Stack.Infrastructure;

namespace Stack.Commands.Helpers;

public class StackStatusProvider : IStackStatusProvider
{
    private readonly ILogger _logger;
    private readonly IGitClient _gitClient;
    private readonly IGitHubClient _gitHubClient;

    public StackStatusProvider(ILogger logger, IGitClient gitClient, IGitHubClient gitHubClient)
    {
        _logger = logger;
        _gitClient = gitClient;
        _gitHubClient = gitHubClient;
    }

    public List<StackStatus> GetStackStatus(List<Config.Stack> stacks, string currentBranch, bool includePullRequestStatus = true)
    {
        var stacksToReturnStatusFor = new List<StackStatus>();

        var stacksOrderedByCurrentBranch = stacks
            .OrderByCurrentStackThenByName(currentBranch);

        var allBranchesInStacks = stacks
            .SelectMany(s => (new[] { s.SourceBranch }).Concat(s.AllBranchNames))
            .Distinct()
            .ToArray();

        var branchStatuses = _gitClient.GetBranchStatuses(allBranchesInStacks);

        if (includePullRequestStatus)
        {
            _logger.Status("Checking status of GitHub pull requests...", () => EvaluateBranchStatusDetails(_logger, _gitClient, _gitHubClient, includePullRequestStatus, stacksToReturnStatusFor, stacksOrderedByCurrentBranch, branchStatuses));
        }
        else
        {
            EvaluateBranchStatusDetails(_logger, _gitClient, _gitHubClient, includePullRequestStatus, stacksToReturnStatusFor, stacksOrderedByCurrentBranch, branchStatuses);
        }

        return stacksToReturnStatusFor;

        static void EvaluateBranchStatusDetails(ILogger logger, IGitClient gitClient, IGitHubClient gitHubClient, bool includePullRequestStatus, List<StackStatus> stacksToReturnStatusFor, IOrderedEnumerable<Config.Stack> stacksOrderedByCurrentBranch, Dictionary<string, GitBranchStatus> branchStatuses)
        {
            foreach (var stack in stacksOrderedByCurrentBranch)
            {
                if (!branchStatuses.TryGetValue(stack.SourceBranch, out var sourceBranchStatus))
                {
                    logger.Warning($"Source branch '{stack.SourceBranch}' does not exist locally or in the remote repository.");
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

    public StackStatus GetStackStatus(Config.Stack stack, string currentBranch, bool includePullRequestStatus = true)
    {
        var statuses = GetStackStatus(new List<Config.Stack> { stack }, currentBranch, includePullRequestStatus);

        return statuses.First();
    }
}
