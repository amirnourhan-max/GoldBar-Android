using System.Security.Cryptography;
using System.Text.Json;

namespace GoldBar.Windows.Core;

public sealed class CredentialStore
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private readonly string _path;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private sealed class CredentialFile
    {
        public string Username { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int Iterations { get; set; } = Iterations;
        public int Version { get; set; } = 1;
    }

    public CredentialStore(string? customPath = null)
    {
        _path = customPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GoldBar", "credentials.json");
    }

    public bool IsRegistered
    {
        get
        {
            try { return Load() is not null; }
            catch { return false; }
        }
    }

    public string RegisteredUsername
    {
        get
        {
            try { return Load()?.Username ?? string.Empty; }
            catch { return string.Empty; }
        }
    }

    public void Register(string username, string password)
    {
        username = (username ?? string.Empty).Trim();
        if (username.Length < 2) throw new ArgumentException("نام کاربری باید حداقل ۲ کاراکتر باشد.");
        if (username.Length > 60) throw new ArgumentException("نام کاربری حداکثر ۶۰ کاراکتر می‌تواند باشد.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            throw new ArgumentException("رمز عبور باید حداقل ۴ کاراکتر باشد.");
        if (password.Length > 128) throw new ArgumentException("رمز عبور بیش از حد طولانی است.");

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        var file = new CredentialFile
        {
            Username = username,
            Salt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(hash),
            Iterations = Iterations,
            Version = 1
        };

        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        var temp = _path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(file, _json));
        File.Move(temp, _path, true);
    }

    public bool Verify(string? username, string? password)
    {
        var file = Load();
        if (file is null || password is null) return false;
        if (!string.Equals(file.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase)) return false;

        try
        {
            var salt = Convert.FromBase64String(file.Salt);
            var expected = Convert.FromBase64String(file.PasswordHash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Math.Max(50_000, file.Iterations), HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }

    private CredentialFile? Load()
    {
        if (!File.Exists(_path)) return null;
        var raw = File.ReadAllText(_path);
        var file = JsonSerializer.Deserialize<CredentialFile>(raw, _json);
        if (file is null || string.IsNullOrWhiteSpace(file.Username) || string.IsNullOrWhiteSpace(file.Salt) || string.IsNullOrWhiteSpace(file.PasswordHash))
            return null;
        return file;
    }
}

public static class CurrentUser
{
    public static string Username { get; set; } = string.Empty;
}
