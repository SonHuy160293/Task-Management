namespace DevFlow.AspNetCore.Options;

public sealed class IntegrationApiOptions
{
    public const string SectionName = "IntegrationApi";

    public string HeaderName { get; init; } = "X-Integration-Key";
    public string[] ApiKeys { get; init; } = [];
}
