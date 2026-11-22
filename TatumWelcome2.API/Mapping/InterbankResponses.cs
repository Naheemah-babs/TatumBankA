using Microsoft.AspNetCore.Mvc;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.API.Mapping
{
    internal static class InterbankResponses
    {
        public static IActionResult Send(InterbankSendResult result)
        {
            InterbankSendResponseDto body = new()
            {
                Message = result.Message,
                Reference = result.Reference,
                Status = result.Outcome.ToString()
            };

            return result.Outcome switch
            {
                InterbankSendOutcome.Successful or
                InterbankSendOutcome.AlreadySuccessful or
                InterbankSendOutcome.AlreadyReversed =>
                    new OkObjectResult(body),

                InterbankSendOutcome.Pending =>
                    new AcceptedResult((string?)null, body),

                InterbankSendOutcome.ReferenceMismatch or
                InterbankSendOutcome.DuplicateReference =>
                    new ConflictObjectResult(body),

                InterbankSendOutcome.AccountLocked =>
    new ObjectResult(body) { StatusCode = StatusCodes.Status429TooManyRequests },

                InterbankSendOutcome.Failed
                    when result.FailureReason == TransactionResult.AccountNotFound =>
                    new NotFoundObjectResult(body),

                _ =>
                    new BadRequestObjectResult(body)
            };
        }

        /// <summary>Plain-text bodies are the bank-to-bank contract: see InterbankProtocol.</summary>
        public static IActionResult Receive(IncomingTransferResult result) =>
            result switch
            {
                IncomingTransferResult.Success =>
                    new OkObjectResult(InterbankProtocol.TransferReceived),

                IncomingTransferResult.AlreadyProcessed =>
                    new OkObjectResult(InterbankProtocol.TransferAlreadyProcessed),

                IncomingTransferResult.Rejected =>
                    new OkObjectResult(InterbankProtocol.TransferRejected),

                IncomingTransferResult.Pending =>
                    new OkObjectResult(InterbankProtocol.TransferPending),

                IncomingTransferResult.AccountNotFound =>
                    new NotFoundObjectResult(InterbankProtocol.RecipientNotFound),

                IncomingTransferResult.ReferenceConflict =>
                    new ConflictObjectResult(InterbankProtocol.ReferenceConflict),

                _ =>
                    new BadRequestObjectResult(InterbankProtocol.InvalidRequest)
            };

        public static IActionResult Status(IncomingTransferOutcome? outcome) =>
            outcome is null
                ? new NotFoundObjectResult("No transfer outcome found for this reference.")
                : new OkObjectResult(outcome);
    }

}
