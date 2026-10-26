namespace DevFlow.SharedKernel.Abstractions.Shared;

public interface IValidationResult
{
    public static readonly Error ValidationError = new(
        "Validation.General",
        "One or more validation errors occurred.");

    Error[] Errors { get; }
}
