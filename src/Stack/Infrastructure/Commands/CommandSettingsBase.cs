using System.ComponentModel;
using Spectre.Console.Cli;
using Stack.Git;

namespace Stack.Commands;

public class CommandSettingsBase : CommandSettings
{
    [Description("Show verbose output.")]
    [CommandOption("--verbose")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }

    [Description("The path to the directory containing the git repository. Defaults to the current directory.")]
    [CommandOption("--working-dir")]
    public string? WorkingDirectory { get; init; }
}

public class CommandWithOutputSettingsBase : CommandSettingsBase
{
    [Description("Output results as JSON.")]
    [CommandOption("--json")]
    [DefaultValue(false)]
    public bool Json { get; init; }

    [Description("Format JSON output with indentation for better readability.")]
    [CommandOption("--pretty")]
    [DefaultValue(false)]
    public bool Pretty { get; init; }
}

public static class CommandSettingsBaseExtensions
{
    public static GitClientSettings GetGitClientSettings(this CommandSettingsBase settings) =>
        new(settings.Verbose, settings.WorkingDirectory);

    public static GitHubClientSettings GetGitHubClientSettings(this CommandSettingsBase settings) =>
        new(settings.Verbose, settings.WorkingDirectory);
}