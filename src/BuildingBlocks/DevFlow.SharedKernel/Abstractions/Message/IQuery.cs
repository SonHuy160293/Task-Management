using DevFlow.SharedKernel.Abstractions.Shared;

namespace DevFlow.SharedKernel.Abstractions.Message;

public interface IQuery<TResponse> : IRequest<Result<TResponse>> { }
