using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Stack.Commands;
using Stack.Infrastructure;

IHost BuildApplication(string[] args)
{
    var settings = new HostApplicationBuilderSettings();
    var builder = Host.CreateEmptyApplicationBuilder(settings);
    builder.ConfigureServices(args);
    builder.ConfigureLogging(args);
    return builder.Build();
}

var host = BuildApplication(args);
await host.StartAsync().ConfigureAwait(false);

try
{
    var rootCommand = host.Services.GetRequiredService<StackRootCommand>();
    var parseResult = rootCommand.Parse(args);

    return await parseResult.InvokeAsync();
}
finally
{
    await host.StopAsync().ConfigureAwait(false);
}
