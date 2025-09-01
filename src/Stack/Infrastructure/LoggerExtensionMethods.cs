using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Stack.Model;

namespace Stack.Infrastructure;

public static partial class LoggerExtensionMethods
{
    public static void NewLine(this ILogger logger) => logger.LogInformation(string.Empty);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Question} {Answer}")]
    [SuppressMessage("LoggerMessage", "LOGGEN036:A value being logged doesn't have an effective way to be converted into a string", Justification = "Usage is only with objects that have ToString() methods")]
    public static partial void SelectedAnswer(this ILogger logger, string question, object answer);

    [LoggerMessage(Level = LogLevel.Information, Message = "No stacks found for current repository.")]
    public static partial void NoStacksForRepository(this ILogger logger);
}
