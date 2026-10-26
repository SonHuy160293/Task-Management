using DevFlow.SharedKernel.Abstractions.Exceptions;

namespace DevFlow.SharedKernel.Exceptions;

public class BadRequestException : DomainException
{
    public BadRequestException(string code, string message)
        : base(code, message) { }

    public BadRequestException(string message)
        : this("BadRequest", message) { }
}
