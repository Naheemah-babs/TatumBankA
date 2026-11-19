using System.Security.Cryptography;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

public class AccountCreationService : IAccountCreationService
{
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
        // Every field is checked, and every problem is reported at once.
        NewAccountValidation validation = NewAccountValidator.Validate(request);

        if (!validation.IsValid)
        {
            return new CreateAccountResultDto(
                CreateAccountStatus.InvalidDetails,
                Errors: validation.Errors);
        }

        // Hash once: PBKDF2 is deliberately slow.
        string pinHash = _pinHasher.Hash(request.Pin);

        for (int attempt = 0; attempt < MaxNumberAttempts; attempt++)
        {
            string number =
                $"00{RandomNumberGenerator.GetInt32(100_000_000):D8}";

            if (_repository.FindByAccountNumber(number) != null)
                continue;

            NewAccount newAccount = new()
            {
                AccountNumber = number,
                Name = validation.Name,
                Phone = validation.Phone,
                Branch = validation.City,
                PinHash = pinHash,
                OpeningDeposit = request.OpeningDeposit,
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