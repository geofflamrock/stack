namespace Stack.Infrastructure;

public interface IDisplayProvider
{
    Task DisplayStatus(string message, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);
    Task<T> DisplayStatus<T>(string message, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
    Task DisplaySuccess(string message, CancellationToken cancellationToken = default);
    Task DisplayStatusWithSuccess(string message, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        return DisplayStatus(message, async ct =>
        {
            await action(ct);
            await DisplaySuccess(message, ct);
        }, cancellationToken);
    }
}
