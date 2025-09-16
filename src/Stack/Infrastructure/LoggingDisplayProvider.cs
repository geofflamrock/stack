using Microsoft.Extensions.Logging;

namespace Stack.Infrastructure;

public class LoggingDisplayProvider(ILogger<LoggingDisplayProvider> logger) : IDisplayProvider
{
    public async Task DisplayStatus(string message, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        logger.Status(message);
        await action(cancellationToken);
    }

    public async Task<T> DisplayStatus<T>(string message, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        logger.Status(message);
        return await action(cancellationToken);
    }

    public async Task DisplaySuccess(string message, CancellationToken cancellationToken = default)
    {
        logger.Success(message);
        await Task.CompletedTask;
    }
}

public static class KnownEvents
{
    public const int Status = 1;
    public const int Success = 2;
}

public static partial class LoggerExtensionMethods
{
    [LoggerMessage(KnownEvents.Status, LogLevel.Information, "{Message}")]
    public static partial void Status(this ILogger logger, string message);

    [LoggerMessage(KnownEvents.Success, LogLevel.Information, "{Message}")]
    public static partial void Success(this ILogger logger, string message);
}
