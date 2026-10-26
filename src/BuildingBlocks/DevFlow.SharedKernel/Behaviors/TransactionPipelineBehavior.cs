using DevFlow.SharedKernel.Abstractions.Message;
using DevFlow.SharedKernel.Abstractions.Persistence;

namespace DevFlow.SharedKernel.Behaviors;

public sealed class TransactionPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IUnitOfWork _unitOfWork;

    public TransactionPipelineBehavior(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!IsCommand())
        {
            return next();
        }

        return _unitOfWork.ExecuteInTransactionAsync(_ => next(), cancellationToken);
    }

    private static bool IsCommand()
        => typeof(ICommand).IsAssignableFrom(typeof(TRequest))
           || typeof(TRequest).GetInterfaces().Any(type =>
               type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommand<>));
}
