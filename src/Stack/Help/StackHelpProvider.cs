using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace Stack.Help;

public class StackHelpProvider : HelpProvider
{
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