using System.Security.Cryptography;

namespace Sensor3.Distribution;

public static class AdminCredentials
{
    public static string Hash(string password)
    {
        if (password.Length < 16) throw new ArgumentException("Använd minst 16 tecken.");
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256:210000:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }
    public static bool Verify(string password, string encoded)
    {
        if (password.Length is < 1 or > 1024) return false;
        try
        {
            var parts = encoded.Split(':');
            if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var iterations) || iterations is < 210000 or > 1000000) return false;
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length < 16 || expected.Length != 32) return false;
            return CryptographicOperations.FixedTimeEquals(expected, Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32));
        }
        catch (FormatException) { return false; }
    }
}
