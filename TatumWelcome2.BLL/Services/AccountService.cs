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

    public LoginResult Login(string accountNumber, string pin)
    {
        if (string.IsNullOrWhiteSpace(accountNumber) ||
            string.IsNullOrWhiteSpace(pin))
        {
            return new LoginResult(LoginOutcome.MissingCredentials);
        }

        Account? account =
            _repository.FindByAccountNumber(accountNumber.Trim());

        if (account == null || !_pinHasher.Verify(pin, account.PinHash))
            return new LoginResult(LoginOutcome.InvalidCredentials);

        return new LoginResult(LoginOutcome.Success, account);
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
        if (!IsValidAmount(amount))
            return TransactionResult.InvalidAmount;

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

        if (!IsValidAmount(amount))
            return TransactionResult.InvalidAmount;

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

        if (senderNumber == recipientNumber)
            return TransactionResult.SameAccount;

        if (!IsValidAmount(amount))
            return TransactionResult.InvalidAmount;

        return _repository.Transfer(
            senderNumber,
            recipientNumber,
            amount,
            pin,
            _referenceGenerator.Generate(ReferencePrefixes.Transfer));
    }

    /// <summary>Positive, at most 2 decimal places, within the balance cap.</summary>
    private static bool IsValidAmount(decimal amount) =>
        amount > 0 &&
        amount <= AccountLimits.MaximumBalance &&
        decimal.Round(amount, 2) == amount;
}
