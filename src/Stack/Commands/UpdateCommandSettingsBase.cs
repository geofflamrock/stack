using System.ComponentModel;
using Spectre.Console.Cli;
using Stack.Git;

namespace Stack.Commands;

internal class UpdateCommandSettingsBase : CommandSettingsBase
{
    [Description("Show what would happen without making any changes.")]
    [CommandOption("--dry-run")]
    [DefaultValue(false)]
    public bool DryRun { get; init; }

    public override GitOperationSettings GetGitOperationSettings() => new(DryRun, Verbose);
}
