using MoreLinq.Experimental;
using Stack.Infrastructure;
using Xunit.Abstractions;

namespace Stack.Tests.Helpers;

public class TestDisplayProvider(ITestOutputHelper testOutputHelper) : IDisplayProvider
{
    public async Task<T> DisplayStatus<T>(string message, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        testOutputHelper.WriteLine($"STATUS: {message}");
        return await action(cancellationToken);
    }

    public async Task DisplayStatus(string message, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        testOutputHelper.WriteLine($"STATUS: {message}");
        await action(cancellationToken);
    }

    public async Task DisplayTree<T>(string header, IEnumerable<TreeItem<T>> items, Func<T, string>? itemFormatter = null, CancellationToken cancellationToken = default)
        where T : notnull
    {
        await Task.CompletedTask;
        testOutputHelper.WriteLine($"TREE: {header}");
        foreach (var item in items)
        {
            testOutputHelper.WriteLine($"  {itemFormatter?.Invoke(item.Value) ?? item.Value.ToString()}");
        }
    }

    public async Task DisplayMessage(string message, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        testOutputHelper.WriteLine($"MESSAGE: {message}");
    }

    public async Task DisplayNewLine(CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        testOutputHelper.WriteLine(string.Empty);
    }

    public async Task DisplayHeader(string header, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        testOutputHelper.WriteLine($"HEADER: {header}");
    }
}
