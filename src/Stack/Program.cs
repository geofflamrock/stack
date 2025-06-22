using System.CommandLine;
using Stack.Commands;

var rootCommand = new StackRootCommand();
var commandLineConfig = new CommandLineConfiguration(rootCommand);

await commandLineConfig.InvokeAsync(args);
