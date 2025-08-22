using Microsoft.Extensions.DependencyInjection;

namespace Stack.Commands;

public class ConfigCommand : GroupCommand
{
    public ConfigCommand(IServiceProvider serviceProvider) : base("config", "Manage stack configuration.", serviceProvider)
    {
        Add(serviceProvider.GetRequiredService<OpenConfigCommand>());
    }
}
