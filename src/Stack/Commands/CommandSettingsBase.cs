using System.ComponentModel;
using Spectre.Console.Cli;
using Stack.Git;

namespace Stack.Commands;

internal class CommandSettingsBase : CommandSettings
{
    [Description("Show verbose output.")]
    [CommandOption("--verbose")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }

    public virtual GitOperationSettings GetGitOperationSettings() => new(false, Verbose);
}
