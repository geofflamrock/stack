using System.CommandLine;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry;
using Stack.Commands;
using Stack.Infrastructure;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

var settings = new HostApplicationBuilderSettings
{
    Configuration = new ConfigurationManager()
};
settings.Configuration.AddEnvironmentVariables();

var hostBuilder = Host.CreateEmptyApplicationBuilder(settings);

hostBuilder.Services
    .AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(Telemetry.ActivitySourceName)
            .ConfigureResource(r =>
            {
                var assembly = Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version?.ToString();

                var assemblyVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

                if (assemblyVersionAttribute is not null)
                {
                    version = assemblyVersionAttribute.InformationalVersion;
                }

                r.AddService(
                    serviceName: "stack-cli",
                    serviceVersion: version);
            });

        if (hostBuilder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] is { })
        {
            tracing.AddOtlpExporter();
        }
    });

using var host = hostBuilder.Build();

await host.StartAsync().ConfigureAwait(false);

var rootCommand = new StackRootCommand();
var commandLineConfig = new CommandLineConfiguration(rootCommand);

using var rootActivity = Telemetry.StartActivity("cli.command", ActivityKind.Server);
rootActivity?.SetTag("cli.args", string.Join(' ', args));

int exitCode = 0;
try
{
    exitCode = await commandLineConfig.InvokeAsync(args);
    rootActivity?.SetTag("cli.exit_code", exitCode);
    if (exitCode == 0)
        rootActivity?.SetStatus(ActivityStatusCode.Ok);
    else
        rootActivity?.SetStatus(ActivityStatusCode.Error, "Non-zero exit code");
}
catch (Exception ex)
{
    rootActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    rootActivity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
    {
        { "exception.type", ex.GetType().FullName },
        { "exception.message", ex.Message },
        { "exception.stacktrace", ex.StackTrace ?? string.Empty }
    }));
    throw;
}
finally
{
    await host.StopAsync().ConfigureAwait(false);
}

return exitCode;
