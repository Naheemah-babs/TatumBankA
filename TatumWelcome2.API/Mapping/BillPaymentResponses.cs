using Microsoft.AspNetCore.Mvc;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.API.Mapping
{
    internal static class BillPaymentResponses
    {
        /// <summary>Provider endpoint (POST /bills/pay).</summary>
        public static IActionResult Pay(BillPaymentResponseDto response) =>
            response.ReferenceConflict
                ? new ConflictObjectResult(response)
                : new OkObjectResult(response);

        /// <summary>Customer airtime/data purchase.</summary>
        public static IActionResult Purchase(BillPurchaseResult result)
        {
            BillPurchaseResponseDto body = new()
            {
                Message = result.Message,
                Reference = result.Reference,
                Amount = result.Amount,
                Status = result.Outcome.ToString(),
                Replayed = result.Replayed
            };

            return result.Outcome switch
            {
                BillPurchaseOutcome.Successful =>
                    new OkObjectResult(body),

                // Valid request, provider said no; nothing was debited.
                BillPurchaseOutcome.Declined =>
                    new UnprocessableEntityObjectResult(body),

                BillPurchaseOutcome.ReferenceConflict =>
                    new ConflictObjectResult(body),

                // Same message and status as a locked login, withdraw or transfer.
                BillPurchaseOutcome.AccountLocked =>
                    new ObjectResult(ApiResponse.Message(
                        AuthResponses.LockedMessage(result.RetryAfterSeconds ?? 0)))
                    {
                        StatusCode = StatusCodes.Status429TooManyRequests
                    },

                // The repository's own PIN re-check found a lock (rare race).
                BillPurchaseOutcome.Rejected
                    when result.FailureReason == TransactionResult.AccountLocked =>
                    new ObjectResult(body)
                    {
                        StatusCode = StatusCodes.Status429TooManyRequests
                    },

                BillPurchaseOutcome.Rejected
                    when result.FailureReason == TransactionResult.AccountNotFound =>
                    new NotFoundObjectResult(body),

                _ =>
                    new BadRequestObjectResult(body)
            };
        }
    }
}