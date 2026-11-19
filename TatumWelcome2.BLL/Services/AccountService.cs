using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

public sealed class AccountService : IAccountService
{
    private readonly IAccountRepository _repository;
    private readonly IPinHasher _pinHasher;
    private readonly ITransactionReferenceGenerator _referenceGenerator;

    public AccountService(
        IAccountRepository repository,
        IPinHasher pinHasher,
        ITransactionReferenceGenerator referenceGenerator)
    {
        _repository = repository;
        _pinHasher = pinHasher;
        _referenceGenerator = referenceGenerator;
    }

    // Hashed once, so an unknown account number costs the same time as a wrong PIN.
    private static string? _dummyHash;

    private string DummyHash() => _dummyHash ??= _pinHasher.Hash("0000");

    public LoginResult Login(string accountNumber, string pin)
    {
        if (string.IsNullOrWhiteSpace(accountNumber) ||
            string.IsNullOrWhiteSpace(pin))
        {
            return new LoginResult(LoginOutcome.MissingCredentials);
        }

        string number = accountNumber.Trim();

        PinAttemptOutcome attempt = _repository.VerifyPinWithLockout(number, pin);

        if (attempt == PinAttemptOutcome.Verified)
        {
            Account? account = _repository.FindByAccountNumber(number);

            return account == null
                ? new LoginResult(LoginOutcome.InvalidCredentials)
                : new LoginResult(LoginOutcome.Success, account);
        }

        if (attempt == PinAttemptOutcome.Locked)
        {
            return new LoginResult(
                LoginOutcome.AccountLocked,
                RetryAfterSeconds: _repository.GetLockSecondsRemaining(number));
        }

        if (attempt == PinAttemptOutcome.AccountNotFound)
        {
            // Same slow work as a real check, so timing can't reveal which accounts exist.
            _pinHasher.Verify(pin, DummyHash());
        }

        // Wrong PIN and unknown account look identical to the caller.
        return new LoginResult(LoginOutcome.InvalidCredentials);
    }

    public AccountLookupDto? LookupAccount(string accountNumber)
    {
        Account? account = _repository.FindByAccountNumber(accountNumber);

        if (account == null)
            return null;

        return new AccountLookupDto
        {
            AccountNumber = account.AccountNumber,
            Name = account.Name
        };
    }

    public BalanceResponseDto? GetBalance(string accountNumber)
    {
        Account? account = _repository.FindByAccountNumber(accountNumber);

        if (account == null)
            return null;

        return new BalanceResponseDto
        {
            AccountNumber = account.AccountNumber,
            Name = account.Name,
            Balance = account.Balance,
            IsFrozen = account.IsFrozen
        };
    }

    public TransactionHistoryResponseDto? GetTransactionHistory(
        string accountNumber)
    {
        Account? account = _repository.FindByAccountNumber(accountNumber);

        if (account == null)
            return null;

        return new TransactionHistoryResponseDto
        {
            AccountNumber = account.AccountNumber,
            Transactions = account.Transactions
                .OrderByDescending(transaction => transaction.Date)
                .Select(transaction => new TransactionItemDto
                {
                    Type = transaction.Type.ToString(),
                    Amount = transaction.Amount,
                    Date = transaction.Date,
                    Description = transaction.Description,
                    Reference = transaction.Reference
                })
                .ToList()
        };
    }

    public TransactionResult Deposit(string accountNumber, decimal amount)
    {
        if (CheckAmount(amount) is { } amountError)
            return amountError;

        return _repository.Deposit(
            accountNumber,
            amount,
            _referenceGenerator.Generate(ReferencePrefixes.Deposit),
            TransactionDescriptions.CashDeposit);
    }

    public TransactionResult Withdraw(
        string accountNumber,
        decimal amount,
        string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
            return TransactionResult.PinRequired;

        if (CheckAmount(amount) is { } amountError)
            return amountError;

        // The PIN is verified against the locked row inside the repository.
        return _repository.Withdraw(
            accountNumber,
            amount,
            pin,
            _referenceGenerator.Generate(ReferencePrefixes.Withdrawal),
            TransactionDescriptions.CashWithdrawal);
    }

    public TransactionResult Transfer(
        string senderNumber,
        string recipientNumber,
        decimal amount,
        string pin)
    {
        if (string.IsNullOrWhiteSpace(recipientNumber))
            return TransactionResult.RecipientRequired;

        if (string.IsNullOrWhiteSpace(pin))
            return TransactionResult.PinRequired;

        // Trim first so "123 " can't slip past the same-account check.
        string recipient = recipientNumber.Trim();

        if (senderNumber == recipient)
            return TransactionResult.SameAccount;

        if (CheckAmount(amount) is { } amountError)
            return amountError;

        return _repository.Transfer(
            senderNumber,
            recipient,
            amount,
            pin,
            _referenceGenerator.Generate(ReferencePrefixes.Transfer));
    }

    /// <summary>
    /// Returns why the amount is rejected, or null when it is acceptable.
    /// The whole-number check runs first so 0.01 gets the "whole number" message.
    /// </summary>
    private static TransactionResult? CheckAmount(decimal amount)
    {
        if (!IsWholeNumber(amount))
            return TransactionResult.AmountNotWholeNumber;

        if (!IsWithinRange(amount))
            return TransactionResult.AmountOutOfRange;

        return null;
    }

    /// <summary>True when the amount has no decimal part (500 and 500.00 pass, 500.50 fails).</summary>
    private static bool IsWholeNumber(decimal amount) =>
        decimal.Truncate(amount) == amount;

    /// <summary>At least the minimum and within the balance cap.</summary>
    private static bool IsWithinRange(decimal amount) =>
        amount >= 500 &&
        amount <= AccountLimits.MaximumBalance;
}