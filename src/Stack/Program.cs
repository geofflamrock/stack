using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Stack.Commands;
using Stack.Infrastructure;

var host = ServiceConfiguration.CreateHost();
await host.StartAsync().ConfigureAwait(false);

try
{
    var rootCommand = host.Services.GetRequiredService<StackRootCommand>();
    var commandLineConfig = new CommandLineConfiguration(rootCommand);

    return await commandLineConfig.InvokeAsync(args);
}
finally
{
    await host.StopAsync().ConfigureAwait(false);
}
