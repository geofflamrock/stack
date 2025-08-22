using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Stack.Commands;
using Stack.Infrastructure;

var host = ServiceConfiguration.CreateHost();
await host.StartAsync();

try
{
    var rootCommand = host.Services.GetRequiredService<StackRootCommand>();
    var commandLineConfig = new CommandLineConfiguration(rootCommand);

    var result = await commandLineConfig.InvokeAsync(args);
    return result;
}
finally
{
    await host.StopAsync();
}
