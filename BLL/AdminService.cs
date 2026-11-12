
using TatumWelcome2.Interfaces;
using TatumWelcome2.Models;

namespace TatumWelcome2.BLL;

public class AdminService
{
    private readonly IAccountRepository _repository;

    public AdminService(IAccountRepository repository)
    {
        _repository = repository;
    }

    public bool FreezeAccount(string accountNumber)
    {
        return _repository.SetFrozenStatus(accountNumber, true);
    }

    public bool UnfreezeAccount(string accountNumber)
    {
        return _repository.SetFrozenStatus(accountNumber, false);
    }

    public Account? FindAccount(string accountNumber)
    {
        return _repository.FindByAccountNumber(accountNumber);
    }
}
