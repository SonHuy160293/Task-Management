using DevFlow.SharedKernel.Abstractions.Shared;

namespace DevFlow.SharedKernel.Abstractions.Message;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse> { }
