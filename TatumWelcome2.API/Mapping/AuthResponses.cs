using Microsoft.AspNetCore.Mvc;
using TatumWelcome2.API.Security;
using TatumWelcome2.BLL.DTOs;

namespace TatumWelcome2.API.Mapping
{
    internal static class AuthResponses
    {
        public static IActionResult AccountCreated(CreateAccountResultDto result) =>
            result switch
            {
                { Status: CreateAccountStatus.Created, Account: { } account } =>
                    new CreatedResult(
                        $"/accounts/{account.AccountNumber}",
                        new CreateAccountResponseDto
                        {
                            Message = "Account created successfully.",
                            AccountNumber = account.AccountNumber,
                            Name = account.Name,
                            Branch = account.Branch,
                            Balance = account.Balance
                        }),

                { Status: CreateAccountStatus.DuplicatePhone } =>
                    ApiResponse.Conflict("This phone number is already registered."),

                { Status: CreateAccountStatus.NumberUnavailable } =>
                    ApiResponse.ServiceUnavailable(
                        "Could not allocate an account number. Please try again."),

                _ =>
                    ApiResponse.BadRequest(
                        "Invalid account details. Check your name, " +
                        "phone number, PIN, city, and opening " +
                        "deposit between 1000 and the maximum " +
                        "supported balance.")
            };

        public static IActionResult LoginFailed(LoginOutcome outcome) =>
            outcome == LoginOutcome.MissingCredentials
                ? ApiResponse.BadRequest("Account number and PIN are required.")
                : ApiResponse.Unauthorized("Invalid account number or PIN.");

        public static IActionResult LoginSucceeded(Account account, AccessToken token) =>
            new OkObjectResult(new LoginResponseDto
            {
                Message = "Login successful.",
                AccountNumber = account.AccountNumber,
                Name = account.Name,
                IsFrozen = account.IsFrozen,
                AccessToken = token.Token,
                TokenType = "Bearer",
                ExpiresAt = token.ExpiresAt
            });
    }
}
