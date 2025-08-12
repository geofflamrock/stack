using System.Text.RegularExpressions;

namespace Stack.Git;

public static class GitBranchStatusParser
{
    static Regex regex = new(
        @"^(?<branchMarker>[\*\+])?\s*(?<branchName>\S+)\s+(?<sha>\S+)(?:\s+\((?<worktreePath>.*?)\))?\s*(\[(?<remoteTrackingBranchName>[^:]+)?(?::\s*(?<status>(ahead\s+(?<ahead>\d+),\s*behind\s+(?<behind>\d+))|(ahead\s+(?<aheadOnly>\d+))|(behind\s+(?<behindOnly>\d+))|(gone)))?\])?\s+(?<message>.+)$",
        RegexOptions.Compiled);

    public static GitBranchStatus? Parse(string branchStatus)
    {
        var match = regex.Match(branchStatus);

        if (match.Success)
        {
            var branchName = match.Groups["branchName"].Value;
            var branchMarker = match.Groups["branchMarker"].Success ? match.Groups["branchMarker"].Value : null;
            var isCurrentBranch = branchMarker == "*";
            var remoteTrackingBranchName = string.IsNullOrEmpty(match.Groups["remoteTrackingBranchName"].Value) ? null : match.Groups["remoteTrackingBranchName"].Value;
            var ahead = match.Groups["ahead"].Success ? int.Parse(match.Groups["ahead"].Value) : (match.Groups["aheadOnly"].Success ? int.Parse(match.Groups["aheadOnly"].Value) : 0);
            var behind = match.Groups["behind"].Success ? int.Parse(match.Groups["behind"].Value) : (match.Groups["behindOnly"].Success ? int.Parse(match.Groups["behindOnly"].Value) : 0);
            var remoteBranchExists = remoteTrackingBranchName is not null && !match.Groups["status"].Value.Contains("gone");
            var sha = match.Groups["sha"].Value;
            var message = match.Groups["message"].Value;

            return new GitBranchStatus(branchName, remoteTrackingBranchName, remoteBranchExists, isCurrentBranch, ahead, behind, new Commit(sha, message));
        }

        return null;
    }
}