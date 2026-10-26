using DevFlow.SharedKernel.Abstractions.Exceptions;

namespace DevFlow.SharedKernel.Exceptions;

public class ForbiddenException : DomainException
{
    public ForbiddenException()
        : this("Access denied.") { }

    public ForbiddenException(string message)
        : base("Forbidden", message) { }
}
