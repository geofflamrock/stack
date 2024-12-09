using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace Stack.Help;

public class StackHelpProvider(ICommandAppSettings settings) : HelpProvider(settings)
{
    readonly Dictionary<string, string[]> KnownGroups = new()
    {
        { CommandGroups.Stack, [CommandNames.New, CommandNames.List, CommandNames.List, CommandNames.Delete, CommandNames.Status] },
        { CommandGroups.Branch, [CommandNames.Switch, CommandNames.Push, CommandNames.Pull, CommandNames.Update, CommandNames.Cleanup, CommandNames.Branch] },
        { CommandGroups.GitHub, [CommandNames.Pr] },
        { CommandGroups.Advanced, [CommandNames.Config] },
    };

    public override IEnumerable<IRenderable> GetHeader(ICommandModel model, ICommandInfo? command)
    {
        if (command is null)
        {
            return
            [
                new Text("Stack is a tool to help manage multiple Git branches that stack on top of each other."),
                Text.NewLine,
                Text.NewLine,
            ];
        }

        return base.GetHeader(model, command);
    }

    public override IEnumerable<IRenderable> GetCommands(ICommandModel model, ICommandInfo? command)
    {
        if (command is null)
        {
            var renderables = new List<IRenderable>();
            var commandGroups = model.Commands.GroupBy(c => KnownGroups.FirstOrDefault(k => k.Value.Contains(c.Name)).Key);

            foreach (var group in commandGroups)
            {
                var groupModel = new GroupCommandModel(model, [.. group]);

                var groupHelp = base.GetCommands(groupModel, command);

                if (groupHelp.Count() > 1)
                {
                    renderables.Add(new Markup($"{Environment.NewLine}[yellow]{(group.Key is not null ? $"{group.Key.ToUpper()} " : "OTHER")}COMMANDS:[/]{Environment.NewLine}"));
                    var commandGrid = (Grid)groupHelp.Last();
                    commandGrid.Columns.First().Width(7);
                    renderables.Add(commandGrid);
                }
            }

            return renderables;
        }

        return base.GetCommands(model, command);
    }

    private class GroupCommandModel(ICommandModel parent, IReadOnlyList<ICommandInfo> commands) : ICommandModel
    {
        public string ApplicationName => parent.ApplicationName;

        public string? ApplicationVersion => parent.ApplicationVersion;

        public IReadOnlyList<string[]> Examples => parent.Examples;

        public IReadOnlyList<ICommandInfo> Commands => commands;

        public ICommandInfo? DefaultCommand => parent.DefaultCommand;
    }
}