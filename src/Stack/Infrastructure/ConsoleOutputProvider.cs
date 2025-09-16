namespace Stack.Infrastructure;

public class ConsoleOutputProvider : IOutputProvider
{
    public async Task WriteLine(string output, CancellationToken cancellationToken = default)
    {
        await Console.Out.WriteLineAsync(output.AsMemory(), cancellationToken);
    }
}
