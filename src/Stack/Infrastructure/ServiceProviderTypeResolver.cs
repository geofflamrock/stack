using Spectre.Console.Cli;

namespace Stack.Infrastructure;

public sealed class ServiceProviderTypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider;

    public ServiceProviderTypeResolver(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public object? Resolve(Type? type)
    {
        if (type == null)
        {
            return null;
        }

        return _provider.GetService(type);
    }

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}