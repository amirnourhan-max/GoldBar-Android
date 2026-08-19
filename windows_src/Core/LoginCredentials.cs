namespace GoldBar.Windows.Core;

public static class LoginCredentials
{
    public const string DefaultUsername = "amirnourhan";
    public const string DefaultPassword = "1234";

    public static bool IsValid(string? username, string? password) =>
        string.Equals(username?.Trim(), DefaultUsername, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(password, DefaultPassword, StringComparison.Ordinal);
}
