namespace DevFlow.AspNetCore.HttpClient.Serialization;

internal sealed record IntegrationResponseError(string Code, string Message, IReadOnlyDictionary<string, string[]>? Errors);
