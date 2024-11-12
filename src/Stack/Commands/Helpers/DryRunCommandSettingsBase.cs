using System.ComponentModel;
using Spectre.Console.Cli;
using Stack.Git;

namespace Stack.Commands;

// A command that supports a dry run
internal class DryRunCommandSettingsBase : CommandSettingsBase
{
    [Description("Show what would happen without making any changes.")]
    [CommandOption("--dry-run")]
    [DefaultValue(false)]
    public bool DryRun { get; init; }

    public override GitOperationSettings GetGitOperationSettings() => new(DryRun, Verbose, WorkingDirectory);

    public override GitHubOperationSettings GetGitHubOperationSettings() => new(DryRun, Verbose, WorkingDirectory);
}
