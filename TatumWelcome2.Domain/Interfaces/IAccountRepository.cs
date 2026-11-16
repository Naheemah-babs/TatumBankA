using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.Domain.Interfaces;

/// <summary>
/// Accounts and their ledger. Money-moving methods lock the affected rows and
/// apply only the checks that must hold under that lock; callers validate
/// input and supply references and descriptions.
/// </summary>
public interface IAccountRepository
{
    Account? FindByAccountNumber(string accountNumber);

    /// <summary>
    /// Inserts the account with its opening balance and records the
    /// opening deposit ledger entry atomically.
    /// </summary>
    AccountCreationResult CreateAccount(NewAccount account);

    /// <summary>
    /// Credits a positive amount. Under the row lock: account exists,
    /// not frozen, balance cap.
    /// </summary>
    TransactionResult Deposit(
        string accountNumber,
        decimal amount,
        string reference,
        string description);

    /// <summary>
    /// Debits a positive amount. Under the row lock: account exists,
    /// PIN, not frozen, sufficient funds.
    /// </summary>
    TransactionResult Withdraw(
        string accountNumber,
        decimal amount,
        string pin,
        string reference,
        string description);

    /// <summary>
    /// Moves a positive amount between two different accounts. Under both
    /// row locks: both exist, sender PIN, sender not frozen, funds,
    /// recipient balance cap.
    /// </summary>
    TransactionResult Transfer(
        string senderNumber,
        string recipientNumber,
        decimal amount,
        string pin,
        string reference);

    /// <summary>False when the account is missing or already in that state.</summary>
    bool SetFrozenStatus(string accountNumber, bool freeze);

    /// <summary>Accounts whose stored PIN is not yet a PBKDF2 hash.</summary>
    IReadOnlyList<LegacyPin> GetLegacyPins();

    /// <summary>Replaces the stored PIN only if it still equals <paramref name="currentValue"/>.</summary>
    bool ReplacePinHash(string accountNumber, string currentValue, string newHash);
}
