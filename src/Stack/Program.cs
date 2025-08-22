using System.CommandLine;
using Stack.Commands;
using Stack.Infrastructure;

var host = ServiceConfiguration.CreateHost();

var rootCommand = new StackRootCommand();
var commandLineConfig = new CommandLineConfiguration(rootCommand);

await commandLineConfig.InvokeAsync(args);
