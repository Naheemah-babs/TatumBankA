
using System.Security.Cryptography;

namespace TatumWelcome2.Services;

public static class TransactionReferenceGenerator
{
    public static string Generate(string prefix)
    {
        string random = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(16));

        return $"{prefix}-{random}";
    }
}
