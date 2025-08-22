using System.CommandLine;
using Stack.Commands;
using Stack.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = ServiceConfiguration.CreateHost();
await host.StartAsync();

try
{
    var rootCommand = host.Services.GetRequiredService<StackRootCommand>();
    var commandLineConfig = new CommandLineConfiguration(rootCommand);
    
    var result = await commandLineConfig.InvokeAsync(args);
    Environment.Exit(result);
}
finally
{
    await host.StopAsync();
}
