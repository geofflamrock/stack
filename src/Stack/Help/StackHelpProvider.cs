using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace Stack.Help;

public static class CommandNames
{
    public const string New = "new";
    public const string List = "list";
    public const string Status = "status";
    public const string Delete = "delete";
    public const string Cleanup = "cleanup";
    public const string Switch = "switch";
    public const string Update = "update";
    public const string Branch = "branch";
    public const string Config = "config";
    public const string Pr = "pr";
}


public static class CommandGroups
{
    public const string Stack = "Stack";
    public const string Branch = "Branch";
    public const string GitHub = "GitHub";
    public const string Advanced = "Advanced";
}

public class StackHelpProvider : HelpProvider
{
    Dictionary<string, string[]> KnownGroups = new Dictionary<string, string[]>
    {
        { CommandGroups.Stack, [CommandNames.New, CommandNames.List, CommandNames.List, CommandNames.Delete] },
        { CommandGroups.Branch, [CommandNames.Switch, CommandNames.Update, CommandNames.Cleanup, CommandNames.Branch] },
        { CommandGroups.GitHub, [CommandNames.Pr] },
        { CommandGroups.Advanced, [CommandNames.Config] },
    };

    public StackHelpProvider(ICommandAppSettings settings) : base(settings)
    {
    }

    // public override IEnumerable<IRenderable> GetHeader(ICommandModel model, ICommandInfo? command)
    // {
    //     if (command is null)
    //     {
    //         return
    //         [
    //             new Text("Stack is a tool to help (me) manage multiple Git branches that stack on top of each other."),
    //             Text.NewLine,
    //             Text.NewLine,
    //         ];
    //     }

    //     return base.GetHeader(model, command);
    // }

    public override IEnumerable<IRenderable> GetCommands(ICommandModel model, ICommandInfo? command)
    {
        if (command is null)
        {
            var renderables = new List<IRenderable>();
            var commandGroups = model.Commands.GroupBy(c => KnownGroups.FirstOrDefault(k => k.Value.Contains(c.Name)).Key);

            foreach (var group in commandGroups)
            {
                renderables.Add(new Rule($"{group.Key.ToUpper()} COMMANDS"));
                foreach (var cmd in group)
                {
                    renderables.Add(new Text($"[yellow]{cmd.Name}[/] - {cmd.Description}"));
                }
            }
        }

        return base.GetCommands(model, command);
    }

    public override IEnumerable<IRenderable> Write(ICommandModel model, ICommandInfo? command)
    {
        if (command is null)
        {
            var renderables = new List<IRenderable>
            {
                new Text("Stack is a tool to help manage multiple Git branches that stack on top of each other."),
                Text.NewLine,
                Text.NewLine,
            };
            renderables.AddRange(base.Write(model, command));
            return renderables;
        }

        return base.Write(model, command);
    }
}