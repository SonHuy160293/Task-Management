namespace DevFlow.SharedKernel.Security;

public interface ICurrentUserProvider
{
    CurrentUser? GetCurrentUser();
}
