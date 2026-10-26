using DevFlow.SharedKernel.Abstractions.Exceptions;

namespace DevFlow.SharedKernel.Exceptions;

public class NotFoundException : DomainException
{
    public NotFoundException(string code, string message)
        : base(code, message) { }

    public NotFoundException(string resourceName, object key)
        : this($"{resourceName}.NotFound", $"{resourceName} '{key}' was not found.") { }
}
