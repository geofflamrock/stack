namespace Stack.Commands;

public abstract class CommandHandlerBase<TInput> where TInput : notnull
{
    public abstract Task Handle(TInput inputs, CancellationToken cancellationToken);
}

public abstract class CommandHandlerBase<TInput, TResponse>
    where TInput : notnull
    where TResponse : notnull
{
    public abstract Task<TResponse> Handle(TInput inputs, CancellationToken cancellationToken);
}