namespace Stack.Infrastructure;

public interface IOutputProvider
{
    Task WriteLine(string output, CancellationToken cancellationToken);

    Task WriteMessage(string message, CancellationToken cancellationToken)
        => WriteLine(RenderingHelpers.RenderMessage(message), cancellationToken);

    Task WriteNewLine(CancellationToken cancellationToken)
        => WriteLine(string.Empty, cancellationToken);

    Task WriteHeader(string header, CancellationToken cancellationToken)
        => WriteLine(RenderingHelpers.RenderHeader(header), cancellationToken);
}
