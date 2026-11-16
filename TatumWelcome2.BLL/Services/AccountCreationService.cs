
using System.Security.Cryptography;
using System.Security.Principal;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

public class AccountCreationService : IAccountCreationService
{
    
    private const decimal MinimumOpeningDeposit = 1000m;
    private const int MaxNumberAttempts = 10;

    private readonly IAccountRepository _repository;
    private readonly IPinHasher _pinHasher;
    private readonly ITransactionReferenceGenerator _referenceGenerator;

    public AccountCreationService(
        IAccountRepository repository,
        IPinHasher pinHasher,
        ITransactionReferenceGenerator referenceGenerator)
    {
        _repository = repository;
        _pinHasher = pinHasher;
        _referenceGenerator = referenceGenerator;
    }

    public CreateAccountResultDto CreateAccount(CreateAccountRequestDto request)
    {
        string name = request.Name ?? string.Empty;
        string phone = request.Phone ?? string.Empty;
        string pin = request.Pin ?? string.Empty;
        string city = request.City ?? string.Empty;
        decimal openingDeposit = request.OpeningDeposit;

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(city) ||
            string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(pin) ||
            phone.Length != 11 ||
            !phone.All(char.IsDigit) ||
            !phone.StartsWith('0') ||
            pin.Length != 4 ||
            !pin.All(char.IsDigit) ||
            openingDeposit < MinimumOpeningDeposit ||
            openingDeposit > AccountLimits.MaximumBalance)
        {
            return new CreateAccountResultDto(CreateAccountStatus.InvalidDetails);
        }

        // Hash once: PBKDF2 is deliberately slow.
        string pinHash = _pinHasher.Hash(pin);

        for (int attempt = 0; attempt < MaxNumberAttempts; attempt++)
        {
            string number =
                $"00{RandomNumberGenerator.GetInt32(100_000_000):D8}";

            if (_repository.FindByAccountNumber(number) != null)
                continue;

            NewAccount newAccount = new()
            {
                AccountNumber = number,
                Name = name.Trim(),
                Phone = phone.Trim(),
                Branch = city.Trim(),
                PinHash = pinHash,
                OpeningDeposit = openingDeposit,
                OpeningDepositReference =
                    _referenceGenerator.Generate(ReferencePrefixes.Deposit),
                OpeningDepositDescription = TransactionDescriptions.OpeningDeposit
            };

            AccountCreationResult result =
                _repository.CreateAccount(newAccount);

            if (result == AccountCreationResult.DuplicatePhone)
                return new CreateAccountResultDto(CreateAccountStatus.DuplicatePhone);

            if (result == AccountCreationResult.DuplicateAccountNumber)
                continue;

            Account account = new()
            {
                AccountNumber = newAccount.AccountNumber,
                Name = newAccount.Name,
                Branch = newAccount.Branch,
                Phone = newAccount.Phone,
                PinHash = newAccount.PinHash,
                Balance = newAccount.OpeningDeposit,
                IsFrozen = false
            };

            return new CreateAccountResultDto(CreateAccountStatus.Created, account);
        }

        return new CreateAccountResultDto(CreateAccountStatus.NumberUnavailable);
    }


}
