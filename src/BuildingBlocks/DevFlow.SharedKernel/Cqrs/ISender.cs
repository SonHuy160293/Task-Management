using DevFlow.SharedKernel.Abstractions.Message;

namespace DevFlow.SharedKernel.Cqrs;

public interface ISender
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
