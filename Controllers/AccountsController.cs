
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TatumWelcome2.DTOs;
using TatumWelcome2.Interfaces;
using TatumWelcome2.Models;
using TatumWelcome2.Security;

namespace TatumWelcome2.Controllers;

[ApiController]
[Route("accounts")]
public class AccountsController : ControllerBase
{
    private readonly IAccountRepository _repository;

    public AccountsController(IAccountRepository repository)
    {
        _repository = repository;
    }

    [InterbankApiKey]
    [HttpGet("{number}")]
    [Tags("Interbank Account Lookup")]
    public IActionResult FindAccount(string number)
    {
        Account? account =
            _repository.FindByAccountNumber(number);

        if (account == null)
            return NotFound("Account not found.");

        return Ok(new AccountLookupDto
        {
            AccountNumber = account.AccountNumber,
            Name = account.Name
        });
    }

    [Authorize]
    [HttpGet("me/balance")]
    [Tags("Customer Banking")]
    public IActionResult GetBalance()
    {
        string? accountNumber = GetAccountNumber();

        if (string.IsNullOrWhiteSpace(accountNumber))
            return Unauthorized();

        Account? account =
            _repository.FindByAccountNumber(accountNumber);

        if (account == null)
            return NotFound(new { Message = "Account not found." });

        return Ok(new
        {
            account.AccountNumber,
            account.Name,
            account.Balance,
            account.IsFrozen
        });
    }

    [Authorize]
    [HttpGet("me/transactions")]
    [Tags("Customer Banking")]
    public IActionResult GetTransactions()
    {
        string? accountNumber = GetAccountNumber();

        if (string.IsNullOrWhiteSpace(accountNumber))
            return Unauthorized();

        Account? account =
            _repository.FindByAccountNumber(accountNumber);

        if (account == null)
            return NotFound(new { Message = "Account not found." });

        return Ok(new
        {
            account.AccountNumber,
            Transactions = account.Transactions
                .OrderByDescending(t => t.Date)
                .Select(t => new
                {
                    Type = t.Type.ToString(),
                    t.Amount,
                    t.Date,
                    t.Description,
                    t.Reference
                })
                .ToList()
        });
    }

    private string? GetAccountNumber()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
    }
}
