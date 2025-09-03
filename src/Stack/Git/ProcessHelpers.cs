using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Stack.Infrastructure;

namespace Stack.Git;

public static class ProcessHelpers
{
    public static string ExecuteProcessAndReturnOutput(
        string fileName,
        string command,
        string? workingDirectory,
        ILogger logger,
        bool captureStandardError = false,
        Func<int, Exception?>? exceptionHandler = null)
    {
        logger.TraceCommand(fileName, command);

        var infoBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = command,
            WorkingDirectory = workingDirectory ?? ".",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null) return string.Empty;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            infoBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            errorBuilder.AppendLine(e.Data);
        };
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        process.WaitForExit();
        int result = process.ExitCode;

        if (result != 0)
        {
            logger.TraceFailedCommand(fileName, command, result, errorBuilder.ToString());

            if (exceptionHandler != null)
            {
                var exception = exceptionHandler(result);
                if (exception != null)
                {
                    throw exception;
                }
            }
            else
            {
                throw new ProcessException(errorBuilder.ToString(), fileName, command, result);
            }
        }

        if (infoBuilder.Length > 0)
        {
            logger.TraceInfoOutput(Markup.Escape(infoBuilder.ToString()));
        }

        var output = infoBuilder.ToString();

        if (captureStandardError)
        {
            output += $"{Environment.NewLine}{errorBuilder}";
        }

        return output;
    }
}

public class ProcessException(string message, string filePath, string command, int exitCode) : Exception(message)
{
    public string FilePath { get; } = filePath;
    public string Command { get; } = command;
    public int ExitCode { get; } = exitCode;
}

internal static partial class LoggerExtensionMethods
{
    [LoggerMessage(Level = LogLevel.Trace, Message = "{FileName} {Command}")]
    public static partial void TraceCommand(this ILogger logger, string fileName, string command);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Failed to execute command: {FileName} {Command}. Exit code: {ExitCode}. Error: {Error}.")]
    public static partial void TraceFailedCommand(this ILogger logger, string fileName, string command, int exitCode, string error);

    [LoggerMessage(Level = LogLevel.Trace, Message = "{Info}")]
    public static partial void TraceInfoOutput(this ILogger logger, string info);
}