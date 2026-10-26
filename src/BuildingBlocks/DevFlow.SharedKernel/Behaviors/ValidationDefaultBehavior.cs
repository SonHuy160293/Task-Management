using DevFlow.SharedKernel.Abstractions.Message;

namespace DevFlow.SharedKernel.Behaviors;

public sealed class ValidationDefaultBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationDefaultBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        foreach (var validator in _validators)
        {
            try
            {
                await validator.ValidateAsync(request, cancellationToken);
            }
            catch (CqrsValidationException exception)
            {
                errors.AddRange(exception.Errors);
            }
        }

        if (errors.Count > 0)
        {
            throw new CqrsValidationException(errors.Distinct());
        }

        return await next();
    }
}
