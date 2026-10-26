using System.Text;
namespace DevFlow.Identity.Application.Security;

public static class PasswordPolicy
{
    public const int MinimumLength = 15;
    public const int MaximumLength = 128;
    public static bool IsValid(string? password)
    {
        if (password is null || password.Length > MaximumLength * 2)
        {
            return false;
        }

        int count = 0;
        for (int index = 0; index < password.Length;)
        {
            if (!Rune.TryGetRuneAt(password, index, out var rune))
            {
                return false;
            }

            count++;
            index += rune.Utf16SequenceLength;
        }

        return count is >= MinimumLength and <= MaximumLength;
    }
}
