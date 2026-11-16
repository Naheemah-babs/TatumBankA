using System.Security.Cryptography;
using System.Text;

namespace TatumWelcome2.API.Security;

    internal static class ApiKeyComparer
    {
        /// <summary>Constant-time comparison so response timing does not leak the key.</summary>
        public static bool Matches(string expected, string? supplied)
        {
            byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
            byte[] suppliedBytes = Encoding.UTF8.GetBytes(supplied ?? string.Empty);

            return expectedBytes.Length == suppliedBytes.Length &&
                   CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
        }
    }


