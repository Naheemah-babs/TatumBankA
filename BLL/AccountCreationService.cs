
using System.Security.Cryptography;
using TatumWelcome2.Interfaces;
using TatumWelcome2.Models;

namespace TatumWelcome2.BLL;

public static class AccountCreationService
{
    private const decimal MaximumBalance = 9999999999999999.99m;

    public static Account? CreateAccount(
        IAccountRepository repository,
        string name,
        string phone,
        string pin,
        string city,
        decimal openingDeposit)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(city) ||
            string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(pin) ||
            phone.Length != 11 ||
            !phone.All(char.IsDigit) ||
            !phone.StartsWith("0") ||
            pin.Length != 4 ||
            !pin.All(char.IsDigit) ||
            openingDeposit < 1000 ||
            openingDeposit > MaximumBalance)
        {
            return null;
        }

        for (int attempt = 0; attempt < 10; attempt++)
        {
            string number =
                $"00{RandomNumberGenerator.GetInt32(100_000_000):D8}";

            if (repository.FindByAccountNumber(number) != null)
                continue;

            Account account = new Account
            {
                Name = name.Trim(),
                Phone = phone.Trim(),
                Branch = city.Trim(),
                Pin = pin,
                AccountNumber = number
            };

            if (!account.Deposit(openingDeposit, "Opening deposit"))
                return null;

            AccountCreationResult result =
                repository.CreateAccount(account);

            if (result == AccountCreationResult.DuplicatePhone)
            {
                throw new InvalidOperationException(
                    "This phone number is already registered.");
            }

            if (result == AccountCreationResult.DuplicateAccountNumber)
                continue;

            return account;
        }

        return null;
    }

    public static Account? CreateAccount(
        List<Account> accounts,
        IAccountRepository repository,
        string name,
        string phone,
        string pin,
        string city,
        decimal openingDeposit)
    {
        Account? account = CreateAccount(
            repository,
            name,
            phone,
            pin,
            city,
            openingDeposit);

        if (account != null)
            accounts.Add(account);

        return account;
    }
}
