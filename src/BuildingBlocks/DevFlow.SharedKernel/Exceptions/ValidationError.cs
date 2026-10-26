namespace DevFlow.SharedKernel.Exceptions;

public sealed record ValidationError(string PropertyName, string ErrorMessage);
