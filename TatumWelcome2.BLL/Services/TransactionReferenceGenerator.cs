
using System.Security.Cryptography;
using TatumWelcome2.BLL.IServices;

namespace TatumWelcome2.BLL.Services;

public sealed class TransactionReferenceGenerator : ITransactionReferenceGenerator
{
    public string Generate(string prefix)
    {
        string random = Convert.ToHexString(
            RandomNumberGenerator.GetBytes(16));

        return $"{prefix}-{random}";
    }
}
