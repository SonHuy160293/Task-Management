namespace DevFlow.Identity.Application.Security;
public enum PasswordVerificationResult
{
    Failed,
    Success,
    SuccessRehashNeeded,
}
