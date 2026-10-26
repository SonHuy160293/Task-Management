using DevFlow.SharedKernel.Abstractions.Shared;

namespace DevFlow.SharedKernel.Abstractions.Message;

public interface ICommand : IRequest<Result> { }

public interface ICommand<TResponse> : IRequest<Result<TResponse>> { }
