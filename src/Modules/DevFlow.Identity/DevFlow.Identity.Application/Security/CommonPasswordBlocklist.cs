namespace DevFlow.Identity.Application.Security;

public static class CommonPasswordBlocklist
{
    private static readonly HashSet<string> Values = new(StringComparer.OrdinalIgnoreCase)
    {
        "passwordpassword", "passwordpasswordpassword", "123456789012345", "1234567890123456",
        "qwertyuiopasdfgh", "correct horse battery staple", "letmeinletmeinletmein",
    };

    public static bool Contains(string password) => Values.Contains(password);
}
