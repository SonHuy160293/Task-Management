namespace DevFlow.SharedKernel.Abstractions.Message;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}

public interface IValidator<in TRequest>
{
    Task ValidateAsync(TRequest request, CancellationToken cancellationToken);
}

public sealed class CqrsValidationException : Exception
{
    public CqrsValidationException(IEnumerable<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyCollection<string> Errors { get; }
}
