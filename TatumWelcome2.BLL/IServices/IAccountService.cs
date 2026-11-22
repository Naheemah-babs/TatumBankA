using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.IServices;

public interface IAccountService
{
    LoginResult Login(string accountNumber, string pin);

    /// <summary>Name lookup for other banks. Null when not found.</summary>
    AccountLookupDto? LookupAccount(string accountNumber);

    BalanceResponseDto? GetBalance(string accountNumber);

    /// <summary>Newest first. Null when the account is not found.</summary>
    TransactionHistoryResponseDto? GetTransactionHistory(string accountNumber);

    /// <summary>Seconds left on a PIN lockout, 0 when the account is not locked.</summary>
    int GetPinLockSecondsRemaining(string accountNumber);

    TransactionResult Deposit(string accountNumber, decimal amount);

    TransactionResult Withdraw(string accountNumber, decimal amount, string pin);

    TransactionResult Transfer(
        string senderNumber,
        string recipientNumber,
        decimal amount,
        string pin);
}
