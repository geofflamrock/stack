using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Stack.Infrastructure;

namespace Stack.Git;

public record ProcessExecutionResult(int ExitCode, string StandardOutput, string StandardError);
public record ProcessExecutionInfo(string FileName, string Arguments, string? WorkingDirectory);

public static class ProcessHelpers
{
    public static ProcessExecutionResult ExecuteProcessAndReturnOutput(
        string fileName,
        string command,
        string? workingDirectory,
        ILogger logger,
        bool captureStandardError = false,
        Action<ProcessExecutionInfo, ProcessExecutionResult>? exceptionHandler = null)
    {
        logger.ExecutingCommand(fileName, command);

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
        if (process is null) return new ProcessExecutionResult(-1, string.Empty, string.Empty);

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
        int exitCode = process.ExitCode;
        var standardOutput = infoBuilder.ToString();
        var standardError = errorBuilder.ToString();
        var result = new ProcessExecutionResult(exitCode, standardOutput, standardError);
        var info = new ProcessExecutionInfo(fileName, command, workingDirectory);

        if (exitCode != 0)
        {
            logger.CommandFailed(fileName, command, exitCode, errorBuilder.ToString());

            if (exceptionHandler != null)
            {
                exceptionHandler(info, result);
            }
            else
            {
                throw new ProcessException(errorBuilder.ToString(), fileName, command, exitCode);
            }
        }

        if (infoBuilder.Length > 0)
        {
            logger.CommandOutput(Markup.Escape(infoBuilder.ToString()));
        }

        var output = infoBuilder.ToString();

        if (captureStandardError)
        {
            output += $"{Environment.NewLine}{errorBuilder}";
        }

        return result;
    }

    public static bool DoesCommandExist(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "where" : "which",
            Arguments = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null) return false;

        process.WaitForExit();
        return process.ExitCode == 0;
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
    public static partial void ExecutingCommand(this ILogger logger, string fileName, string command);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Failed to execute command: {FileName} {Command}. Exit code: {ExitCode}. Error: {Error}.")]
    public static partial void CommandFailed(this ILogger logger, string fileName, string command, int exitCode, string error);

    [LoggerMessage(Level = LogLevel.Trace, Message = "{Info}")]
    public static partial void CommandOutput(this ILogger logger, string info);
}