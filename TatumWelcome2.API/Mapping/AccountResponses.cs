using Microsoft.AspNetCore.Mvc;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.API.Mapping
{
    internal static class AccountResponses
    {
        /// <summary>Interbank lookup: plain-text 404 is part of the bank-to-bank contract.</summary>
        public static IActionResult Lookup(AccountLookupDto? account) =>
            account is null
                ? new NotFoundObjectResult("Account not found.")
                : new OkObjectResult(account);

        public static IActionResult Balance(BalanceResponseDto? balance) =>
            balance is null
                ? ApiResponse.NotFound("Account not found.")
                : new OkObjectResult(balance);

        public static IActionResult History(TransactionHistoryResponseDto? history) =>
            history is null
                ? ApiResponse.NotFound("Account not found.")
                : new OkObjectResult(history);
    }
}
