using DevFlow.SharedKernel.Abstractions.Exceptions;

namespace DevFlow.SharedKernel.Exceptions;

public sealed class ServiceUnavailableException : DomainException
{
    public ServiceUnavailableException(string code, string message)
        : base(code, message)
    {
    }
}
