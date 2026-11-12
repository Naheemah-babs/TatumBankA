
using TatumWelcome2.Interfaces;
using TatumWelcome2.Models;
using TatumWelcome2.Services;

namespace TatumWelcome2.BLL;

public class AccountService
{
    public static Account? FindAccount(
        List<Account> accounts,
        string accountNumber)
    {
        return accounts.FirstOrDefault(
            account => account.AccountNumber == accountNumber);
    }

    public static bool VerifyPin(Account account, string pin)
    {
        if (account.Pin.StartsWith("PBKDF2$"))
            return PinHasher.Verify(pin, account.Pin);

        return account.Pin == pin;
    }

    public static Account? Login(
        IAccountRepository repository,
        string accountNumber,
        string pin)
    {
        Account? account =
            repository.FindByAccountNumber(accountNumber.Trim());

        if (account == null || !VerifyPin(account, pin))
            return null;

        return account;
    }

    public static TransactionResult Transfer(
        Account from,
        Account to,
        decimal amount)
    {
        return Account.TransferBetween(from, to, amount);
    }
}
