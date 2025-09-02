using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Stack.Infrastructure;

public static partial class LoggerExtensionMethods
{
    public static void NewLine(this ILogger logger) => logger.LogInformation(string.Empty);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Question}")]
    public static partial void Question(this ILogger logger, string question);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Question} {Answer}")]
    [SuppressMessage("LoggerMessage", "LOGGEN036:A value being logged doesn't have an effective way to be converted into a string", Justification = "Usage is only with objects that have ToString() methods")]
    public static partial void Answer(this ILogger logger, string question, object answer);

    [LoggerMessage(Level = LogLevel.Information, Message = "No stacks found for current repository.")]
    public static partial void NoStacksForRepository(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pushing branch {Branch} to remote repository.")]
    public static partial void PushingBranch(this ILogger logger, string branch);

    [LoggerMessage(Level = LogLevel.Warning, Message = "An error has occurred pushing branch {Branch} to remote repository. Run 'stack push --name \"{Stack}\"' to push the branch to the remote repository.")]
    public static partial void NewBranchPushWarning(this ILogger logger, string branch, string stack);

    [LoggerMessage(Level = LogLevel.Warning, Message = "An error has occurred changing to branch {Branch}. Run 'stack switch --branch \"{Branch}\"' to switch to the branch. Error: {ErrorMessage}.")]
    public static partial void ChangeBranchWarning(this ILogger logger, string branch, string errorMessage);
}
