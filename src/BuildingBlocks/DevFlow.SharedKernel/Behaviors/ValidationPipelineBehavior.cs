using DevFlow.SharedKernel.Abstractions.Message;
using DevFlow.SharedKernel.Abstractions.Shared;

namespace DevFlow.SharedKernel.Behaviors;

public sealed class ValidationPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationPipelineBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var errors = new List<Error>();

        foreach (var validator in _validators)
        {
            try
            {
                await validator.ValidateAsync(request, cancellationToken);
            }
            catch (CqrsValidationException exception)
            {
                errors.AddRange(exception.Errors.Select(error => new Error("Validation.Error", error)));
            }
        }

        if (errors.Count > 0)
        {
            return CreateValidationResult<TResponse>(errors.Distinct().ToArray());
        }

        return await next();
    }

    private static TResult CreateValidationResult<TResult>(Error[] errors)
        where TResult : Result
    {
        if (typeof(TResult) == typeof(Result))
        {
            return (ValidationResult.WithErrors(errors) as TResult)!;
        }

        var valueType = typeof(TResult).GenericTypeArguments[0];
        var validationResult = typeof(ValidationResult<>)
            .MakeGenericType(valueType)
            .GetMethod(nameof(ValidationResult<object>.WithErrors))!
            .Invoke(null, [errors])!;

        return (TResult)validationResult;
    }
}
