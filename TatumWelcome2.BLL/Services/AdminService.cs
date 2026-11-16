using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Interfaces;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.Services;

public sealed class AdminService : IAdminService
{
    private readonly IAccountRepository _repository;

    public AdminService(IAccountRepository repository)
    {
        _repository = repository;
    }

    public AdminAccountResponseDto? GetAccountSummary(string accountNumber)
    {
        Account? account = _repository.FindByAccountNumber(accountNumber);

        if (account == null)
            return null;

        return new AdminAccountResponseDto
        {
            AccountNumber = account.AccountNumber,
            Name = account.Name,
            IsFrozen = account.IsFrozen
        };
    }

    public AccountStatusChangeResult Freeze(string accountNumber)
    {
        return ChangeFrozenStatus(accountNumber, freeze: true);
    }

    public AccountStatusChangeResult Unfreeze(string accountNumber)
    {
        return ChangeFrozenStatus(accountNumber, freeze: false);
    }

    private AccountStatusChangeResult ChangeFrozenStatus(
        string accountNumber,
        bool freeze)
    {
        Account? account = _repository.FindByAccountNumber(accountNumber);

        if (account == null)
            return AccountStatusChangeResult.AccountNotFound;

        if (account.IsFrozen == freeze)
            return AccountStatusChangeResult.AlreadyInState;

        // The UPDATE is guarded on the current state, so a concurrent change
        // makes it affect no rows.
        return _repository.SetFrozenStatus(accountNumber, freeze)
            ? AccountStatusChangeResult.Changed
            : AccountStatusChangeResult.UpdateFailed;
    }
}
