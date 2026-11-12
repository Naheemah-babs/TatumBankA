
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using TatumWelcome2.DTOs;
using TatumWelcome2.Interfaces;

namespace TatumWelcome2.Controllers;

[ApiController]
[Authorize]
[Route("accounts/me")]
[Tags("Customer Transactions")]
public class TransactionsController : ControllerBase
{
    private readonly IAccountRepository _repository;

    public TransactionsController(IAccountRepository repository)
    {
        _repository = repository;
    }

    [HttpPost("deposit")]
    public IActionResult Deposit(
        [FromBody] DepositRequestDto request)
    {
        string? accountNumber = GetAccountNumber();

        if (string.IsNullOrWhiteSpace(accountNumber))
            return Unauthorized();

        TransactionResult result =
            _repository.Deposit(accountNumber, request.Amount);

        return result switch
        {
            TransactionResult.Success =>
                Ok(new
                {
                    Message = "Deposit successful.",
                    request.Amount,
                    AccountNumber = accountNumber
                }),

            TransactionResult.InvalidAmount =>
                BadRequest(new
                {
                    Message = "Deposit amount must be greater than zero."
                }),

            TransactionResult.AccountNotFound =>
                NotFound(new { Message = "Account not found." }),

            TransactionResult.AccountFrozen =>
                BadRequest(new { Message = "Account is frozen." }),

            TransactionResult.BalanceLimitExceeded =>
                BadRequest(new
                {
                    Message =
                        "Deposit failed. Account balance limit " +
                        "would be exceeded."
                }),

            _ => BadRequest(new { Message = "Deposit failed." })
        };
    }

    [HttpPost("withdraw")]
    [EnableRateLimiting("pin-transactions")]
    public IActionResult Withdraw(
        [FromBody] WithdrawRequestDto request)
    {
        string? accountNumber = GetAccountNumber();

        if (string.IsNullOrWhiteSpace(accountNumber))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Pin))
            return BadRequest(new { Message = "PIN is required." });

        TransactionResult result = _repository.Withdraw(
            accountNumber,
            request.Amount,
            request.Pin);

        return result switch
        {
            TransactionResult.Success =>
                Ok(new
                {
                    Message = "Withdrawal successful.",
                    request.Amount
                }),

            TransactionResult.InvalidPin =>
                BadRequest(new { Message = "Incorrect PIN." }),

            TransactionResult.AccountFrozen =>
                BadRequest(new { Message = "Account is frozen." }),

            TransactionResult.InsufficientFunds =>
                BadRequest(new { Message = "Insufficient funds." }),

            TransactionResult.InvalidAmount =>
                BadRequest(new
                {
                    Message = "Withdrawal amount must be greater than zero."
                }),

            TransactionResult.AccountNotFound =>
                NotFound(new { Message = "Account not found." }),

            _ => BadRequest(new { Message = "Withdrawal failed." })
        };
    }

    [HttpPost("transfers")]
    [EnableRateLimiting("pin-transactions")]
    public IActionResult Transfer(
        [FromBody] IntrabankTransferRequestDto request)
    {
        string? senderNumber = GetAccountNumber();

        if (string.IsNullOrWhiteSpace(senderNumber))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(
            request.RecipientAccountNumber))
        {
            return BadRequest(new
            {
                Message = "Recipient account number is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Pin))
            return BadRequest(new { Message = "PIN is required." });

        TransactionResult result = _repository.Transfer(
            senderNumber,
            request.RecipientAccountNumber,
            request.Amount,
            request.Pin);

        return result switch
        {
            TransactionResult.Success =>
                Ok(new
                {
                    Message = "Transfer successful.",
                    request.RecipientAccountNumber,
                    request.Amount
                }),

            TransactionResult.SameAccount =>
                BadRequest(new
                {
                    Message = "You cannot transfer to your own account."
                }),

            TransactionResult.InvalidPin =>
                BadRequest(new { Message = "Incorrect PIN." }),

            TransactionResult.AccountFrozen =>
                BadRequest(new { Message = "Account is frozen." }),

            TransactionResult.InsufficientFunds =>
                BadRequest(new { Message = "Insufficient funds." }),

            TransactionResult.InvalidAmount =>
                BadRequest(new
                {
                    Message = "Transfer amount must be greater than zero."
                }),

            TransactionResult.InvalidRecipient =>
                BadRequest(new
                {
                    Message = "Recipient account not found."
                }),

            TransactionResult.AccountNotFound =>
                NotFound(new
                {
                    Message = "Sender account not found."
                }),

            TransactionResult.BalanceLimitExceeded =>
                BadRequest(new
                {
                    Message =
                        "Transfer failed. Recipient account " +
                        "balance limit would be exceeded."
                }),

            _ => BadRequest(new { Message = "Transfer failed." })
        };
    }

    private string? GetAccountNumber()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
    }
}
