
using System.Security.Cryptography;
using TatumWelcome2.BLL.IServices;

namespace TatumWelcome2.BLL.Services;

public sealed class PinHasher : IPinHasher
{
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;


    public string Hash(string pin)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            pin,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool Verify(string pin, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(pin) ||
            string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        try
        {
            string[] parts = storedHash.Split('$');

            if (parts.Length != 4 || parts[0] != "PBKDF2")
                return false;

            if (!int.TryParse(parts[1], out int iterations) ||
                iterations != Iterations)
                return false;

            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expectedHash = Convert.FromBase64String(parts[3]);

            if (salt.Length != SaltSize ||
                expectedHash.Length != HashSize)
                return false;

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                pin,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(
                actualHash,
                expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
