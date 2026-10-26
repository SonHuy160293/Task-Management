namespace DevFlow.SharedKernel.Security;

public static class CustomClaims
{
    public const string SessionId = "sid";
    public const string Permission = nameof(Permission);
    public const string TenantIds = nameof(TenantIds);
    public const string BranchIds = nameof(BranchIds);
}
