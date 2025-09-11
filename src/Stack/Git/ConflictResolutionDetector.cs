using Microsoft.Extensions.Logging;

namespace Stack.Git;

public enum ConflictOperationType { Merge, Rebase }
public enum ConflictResolutionResult { Completed, Aborted, NotStarted, Timeout }

public static class ConflictResolutionDetector
{
    public static async Task<ConflictResolutionResult> WaitForConflictResolution(
        IGitClient gitClient,
        ILogger logger,
        ConflictOperationType operationType,
        TimeSpan pollInterval,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        var isInProgress = operationType == ConflictOperationType.Merge
            ? gitClient.IsMergeInProgress()
            : gitClient.IsRebaseInProgress();

        if (!isInProgress)
        {
            return ConflictResolutionResult.NotStarted;
        }

        var operationTypeLowercase = operationType.ToString().ToLowerInvariant();

        logger.LogInformation("Conflicts detected during {Operation}. Please resolve conflicts to continue or press CTRL+C to abort...", operationTypeLowercase);

        // For a rebase, HEAD moves to the upstream branch during conflicts, so comparing
        // current HEAD to the starting HEAD is unreliable. Git stores the previous HEAD
        // in ORIG_HEAD before starting the rebase; prefer that for detecting abort vs complete.
        var initialHead = operationType == ConflictOperationType.Rebase
            ? gitClient.GetOriginalHeadSha()
            : gitClient.GetHeadSha();

        if (string.IsNullOrEmpty(initialHead))
        {
            logger.LogWarning("Could not determine initial HEAD SHA before {Operation}. Unable to detect if operation was completed or aborted.", operationTypeLowercase);
            return ConflictResolutionResult.NotStarted;
        }

        var pollCount = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (timeout.HasValue && DateTime.UtcNow - startTime >= timeout.Value)
            {
                logger.LogWarning("Timed out waiting for {Operation} conflict resolution after {Timeout}.", operationTypeLowercase, timeout);
                return ConflictResolutionResult.Timeout;
            }

            // Check if operation still in progress
            isInProgress = operationType == ConflictOperationType.Merge
                ? gitClient.IsMergeInProgress()
                : gitClient.IsRebaseInProgress();

            if (!isInProgress)
            {
                var currentHead = gitClient.GetHeadSha();
                // If HEAD differs from the original (ORIG_HEAD for rebase), we consider operation completed.
                if (!string.Equals(initialHead, currentHead, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("{Operation} conflicts resolved", operationType);
                    return ConflictResolutionResult.Completed;
                }
                else
                {
                    logger.LogInformation("{Operation} has been aborted", operationType);
                    return ConflictResolutionResult.Aborted;
                }
            }

            // Still in progress; increment poll counter and emit a debug log every 5 polls
            pollCount++;
            if (pollCount % 5 == 0)
            {
                logger.LogDebug("{Operation} still in progress...", operationType);
            }

            await Task.Delay(pollInterval, cancellationToken);
        }
    }
}