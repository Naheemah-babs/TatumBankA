using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TatumWelcome2.API.Mapping;
using TatumWelcome2.API.Security;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Security;

namespace TatumWelcome2.API.Controllers;

[ApiController]
[Route("accounts")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [InterbankApiKey]
    [HttpGet("{number}")]
    [Tags("Interbank Account Lookup")]
    public IActionResult FindAccount(string number)
    {
        return AccountResponses.Lookup(_accountService.LookupAccount(number));
    }

    [Authorize]
    [HttpGet("me/balance")]
    [Tags("Customer Banking")]
    public IActionResult GetBalance()
    {
        string? accountNumber = User.GetAccountNumber();

        if (accountNumber is null)
            return Unauthorized();

        return AccountResponses.Balance(_accountService.GetBalance(accountNumber));
    }

    [Authorize]
    [HttpGet("me/transactions")]
    [Tags("Customer Banking")]
    public IActionResult GetTransactions()
    {
        string? accountNumber = User.GetAccountNumber();

        if (accountNumber is null)
            return Unauthorized();

        return AccountResponses.History(
            _accountService.GetTransactionHistory(accountNumber));
    }
}
