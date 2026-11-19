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

                { Status: CreateAccountStatus.InvalidDetails, Errors: { Count: > 0 } errors } =>
                    new BadRequestObjectResult(new
                    {
                        message = "Some account details are invalid.",
                        errors = errors.Select(error => new
                        {
                            field = error.Field,
                            message = error.Message
                        })
                    }),

                { Status: CreateAccountStatus.DuplicatePhone } =>
                    ApiResponse.Conflict("This phone number is already registered."),

                { Status: CreateAccountStatus.NumberUnavailable } =>
                    ApiResponse.ServiceUnavailable(
                        "Could not allocate an account number. Please try again."),

                _ => ApiResponse.BadRequest("Invalid account details.")
            };

        public static IActionResult LoginFailed(LoginResult result) =>
    result.Outcome switch
    {
        LoginOutcome.MissingCredentials =>
            ApiResponse.BadRequest("Account number and PIN are required."),

        LoginOutcome.AccountLocked =>
            new ObjectResult(new
            {
                message = LockedMessage(result.RetryAfterSeconds)
            })
            { StatusCode = 429 },

        _ => ApiResponse.Unauthorized("Incorrect account number or PIN.")
    };

        private static string LockedMessage(int seconds)
        {
            int minutes = Math.Max(1, (int)Math.Ceiling(seconds / 60.0));

            return "Too many incorrect attempts. Try again in " +
                   (minutes == 1 ? "1 minute." : $"{minutes} minutes.");
        }

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