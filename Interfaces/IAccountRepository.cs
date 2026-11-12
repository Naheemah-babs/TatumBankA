
using TatumWelcome2.Models;

namespace TatumWelcome2.Interfaces;

public interface IAccountRepository
{
    Account? FindByAccountNumber(string accountNumber);

    AccountCreationResult CreateAccount(Account account);

    TransactionResult Deposit(string accountNumber, decimal amount);

    TransactionResult Withdraw(
        string accountNumber,
        decimal amount,
        string pin);

    TransactionResult Transfer(
        string senderNumber,
        string recipientNumber,
        decimal amount,
        string pin);

    bool SetFrozenStatus(string accountNumber, bool freeze);
}
