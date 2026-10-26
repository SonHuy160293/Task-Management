namespace DevFlow.Identity.Contracts;

public sealed record UserIntegrationDto(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    string Status);
