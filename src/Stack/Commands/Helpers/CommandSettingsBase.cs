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

    public virtual GitOperationSettings GetGitOperationSettings() => new(false, Verbose, WorkingDirectory);
    public virtual GitHubOperationSettings GetGitHubOperationSettings() => new(false, Verbose, WorkingDirectory);
}
