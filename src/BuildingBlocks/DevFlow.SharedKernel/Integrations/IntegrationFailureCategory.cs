namespace DevFlow.SharedKernel.Integrations;

public enum IntegrationFailureCategory
{
    None,
    Downstream,
    Authentication,
    Authorization,
    Validation,
    NotFound,
    Conflict,
    RateLimited,
    Timeout,
    CircuitOpen,
    Network,
    InvalidResponse,
}
