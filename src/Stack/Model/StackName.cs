using Spectre.Console;
using Stack.Infrastructure;
using Vogen;

namespace Stack.Model;

[ValueObject<string>]
public partial class StackName : IAnsiConsoleFormattable
{
    public string Format()
    {
        return Value.Stack();
    }
}

[ValueObject<string>]
public partial class BranchName : IAnsiConsoleFormattable
{
    public string Format()
    {
        return Value.Branch();
    }
}

[ValueObject<string>]
public partial class Example : IAnsiConsoleFormattable
{
    public string Format()
    {
        return Value.Example();
    }
}