using System.ComponentModel;
using Spectre.Console.Cli;
using Stack.Git;

namespace Stack.Commands;

// A command that supports a dry run
public class DryRunCommandSettingsBase : CommandSettingsBase
{
    [Description("Show what would happen without making any changes.")]
    [CommandOption("--dry-run")]
    [DefaultValue(false)]
    public bool DryRun { get; init; }

    public override GitClientSettings GetGitClientSettings() => new(DryRun, Verbose, WorkingDirectory);

    public override GitHubOperationSettings GetGitHubOperationSettings() => new(DryRun, Verbose, WorkingDirectory);
}
