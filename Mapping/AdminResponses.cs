using Microsoft.AspNetCore.Mvc;
using TatumWelcome2.BLL.DTOs;

namespace TatumWelcome2.API.Mapping
{
    internal static class AdminResponses
    {
        public static IActionResult AccountSummary(AdminAccountResponseDto? account) =>
            account is null
                ? ApiResponse.NotFound("Account not found.")
                : new OkObjectResult(account);

        public static IActionResult StatusChange(
            AccountStatusChangeResult result,
            string accountNumber,
            bool freeze) =>
            result switch
            {
                AccountStatusChangeResult.Changed =>
                    new OkObjectResult(new AccountStatusResponseDto
                    {
                        Message = freeze
                            ? "Account frozen successfully."
                            : "Account unfrozen successfully.",
                        AccountNumber = accountNumber
                    }),

                AccountStatusChangeResult.AlreadyInState =>
                    new OkObjectResult(new AccountStatusResponseDto
                    {
                        Message = freeze
                            ? "Account is already frozen."
                            : "Account is already active."
                    }),

                AccountStatusChangeResult.AccountNotFound =>
                    ApiResponse.NotFound("Account not found."),

                _ =>
                    ApiResponse.Conflict("Account status could not be updated.")
            };

        public static IActionResult PaymentMode(string mode) =>
            new OkObjectResult(new PaymentModeResponseDto { PaymentMode = mode });

        public static IActionResult PaymentModeUpdated(string mode) =>
            new OkObjectResult(new PaymentModeResponseDto
            {
                Message = "Payment mode updated successfully.",
                PaymentMode = mode
            });

        public static IActionResult InvalidPaymentMode() =>
            ApiResponse.BadRequest("Invalid mode. Choose Random, Success, or Failure.");
    }
}
