using Microsoft.Extensions.Logging;

namespace Stack.Infrastructure;

public static class LoggerExtensionMethods
{
    public static void NewLine(this ILogger logger) => logger.LogInformation(string.Empty);
}
