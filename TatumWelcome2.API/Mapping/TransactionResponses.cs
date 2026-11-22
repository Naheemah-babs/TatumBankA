using Microsoft.AspNetCore.Mvc;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.API.Mapping
{
    internal static class TransactionResponses
    {
        public static IActionResult Deposit(
            TransactionResult result,
            string accountNumber,
            decimal amount) =>
            result switch
            {
                TransactionResult.Success =>
                    new OkObjectResult(new DepositResponseDto
                    {
                        Message = "Deposit successful.",
                        Amount = amount,
                        AccountNumber = accountNumber
                    }),

                TransactionResult.InvalidAmount =>
                    ApiResponse.BadRequest("Invalid amount."),

                TransactionResult.AmountNotWholeNumber => NotWholeNumber(),

                TransactionResult.AmountOutOfRange => OutOfRange(),

                TransactionResult.AccountNotFound =>
                    ApiResponse.NotFound("Account not found."),

                TransactionResult.AccountFrozen =>
                    ApiResponse.BadRequest("Account is frozen."),

                TransactionResult.BalanceLimitExceeded =>
                    ApiResponse.BadRequest(
                        "Deposit failed. Account balance limit would be exceeded."),

                _ => ApiResponse.BadRequest("Deposit failed.")
            };

        public static IActionResult Withdraw(
            TransactionResult result,
            decimal amount,
            int lockSeconds = 0) =>
            result switch
            {
                TransactionResult.Success =>
                    new OkObjectResult(new WithdrawalResponseDto
                    {
                        Message = "Withdrawal successful.",
                        Amount = amount
                    }),

                TransactionResult.PinRequired =>
                    ApiResponse.BadRequest("PIN is required."),

                TransactionResult.InvalidPin =>
                    ApiResponse.BadRequest("Incorrect PIN."),

                TransactionResult.AccountLocked => Locked(lockSeconds),

                TransactionResult.AccountFrozen =>
                    ApiResponse.BadRequest("Account is frozen."),

                TransactionResult.InsufficientFunds =>
                    ApiResponse.BadRequest("Insufficient funds."),

                TransactionResult.InvalidAmount =>
                    ApiResponse.BadRequest("Withdrawal amount must be greater than zero."),

                TransactionResult.AmountNotWholeNumber => NotWholeNumber(),

                TransactionResult.AmountOutOfRange => OutOfRange(),

                TransactionResult.AccountNotFound =>
                    ApiResponse.NotFound("Account not found."),

                _ => ApiResponse.BadRequest("Withdrawal failed.")
            };

        public static IActionResult Transfer(
            TransactionResult result,
            string recipientAccountNumber,
            decimal amount,
            int lockSeconds = 0) =>
            result switch
            {
                TransactionResult.Success =>
                    new OkObjectResult(new TransferResponseDto
                    {
                        Message = "Transfer successful.",
                        RecipientAccountNumber = recipientAccountNumber,
                        Amount = amount
                    }),

                TransactionResult.RecipientRequired =>
                    ApiResponse.BadRequest("Recipient account number is required."),

                TransactionResult.PinRequired =>
                    ApiResponse.BadRequest("PIN is required."),

                TransactionResult.SameAccount =>
                    ApiResponse.BadRequest("You cannot transfer to your own account."),

                TransactionResult.InvalidPin =>
                    ApiResponse.BadRequest("Incorrect PIN."),

                TransactionResult.AccountLocked => Locked(lockSeconds),

                TransactionResult.AccountFrozen =>
                    ApiResponse.BadRequest("Account is frozen."),

                TransactionResult.InsufficientFunds =>
                    ApiResponse.BadRequest("Insufficient funds."),

                TransactionResult.InvalidAmount =>
                    ApiResponse.BadRequest("Transfer amount must be greater than zero."),

                TransactionResult.AmountNotWholeNumber => NotWholeNumber(),

                TransactionResult.AmountOutOfRange => OutOfRange(),

                TransactionResult.InvalidRecipient =>
                    ApiResponse.BadRequest("Recipient account not found."),

                TransactionResult.AccountNotFound =>
                    ApiResponse.NotFound("Sender account not found."),

                TransactionResult.BalanceLimitExceeded =>
                    ApiResponse.BadRequest(
                        "Transfer failed. Recipient account balance limit would be exceeded."),

                _ => ApiResponse.BadRequest("Transfer failed.")
            };

        // Shared by deposit, withdraw and transfer, so the wording is identical.
        private static IActionResult NotWholeNumber() =>
            ApiResponse.BadRequest("Amount must be a whole number, with no decimal places.");

        private static IActionResult OutOfRange() =>
            ApiResponse.BadRequest(
                $"Amount must be at least 500 and not more than {AccountLimits.MaximumBalance:N0}.");

        // Same message and status as a locked login.
        private static IActionResult Locked(int seconds) =>
            new ObjectResult(ApiResponse.Message(AuthResponses.LockedMessage(seconds)))
            {
                StatusCode = StatusCodes.Status429TooManyRequests
            };
    }
}
