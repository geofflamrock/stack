using System.Diagnostics;
using System.Text;
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
        bool verbose = false,
        bool captureStandardError = false,
        Func<int, Exception?>? exceptionHandler = null)
    {
        if (verbose)
            logger.Debug($"{fileName} {command}");

        using var activity = Telemetry.StartActivity("process.exec");
        activity?.SetTag("process.executable.name", fileName);
        activity?.SetTag("process.command_line", $"{fileName} {command}");
        if (workingDirectory is not null)
            activity?.SetTag("process.working_directory", workingDirectory);

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

        activity?.SetTag("process.exit_code", result);

        if (result != 0)
        {
            if (verbose)
                logger.Debug($"Failed to execute command: {fileName} {command}. Exit code: {result}. Error: {errorBuilder}.");

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
                var message = errorBuilder.ToString();
                activity?.SetStatus(ActivityStatusCode.Error, message);
                activity?.AddEvent(new ActivityEvent("process.error"));
                throw new ProcessException(message, result);
            }
        }

        if (verbose && infoBuilder.Length > 0)
        {
            logger.Debug(Markup.Escape(infoBuilder.ToString()));
        }

        var output = infoBuilder.ToString();

        if (captureStandardError)
        {
            output += $"{Environment.NewLine}{errorBuilder}";
        }

        return output;
    }
}

public class ProcessException : Exception
{
    public int ExitCode { get; }

    public ProcessException(string message, int exitCode) : base(message)
    {
        ExitCode = exitCode;
    }
}