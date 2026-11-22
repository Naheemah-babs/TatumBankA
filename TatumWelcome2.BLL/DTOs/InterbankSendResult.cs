using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.BLL.DTOs;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.DTOs
{
    public enum InterbankSendOutcome
    {
        /// <summary>Receiving bank confirmed the credit.</summary>
        Successful,

        /// <summary>Sender debited; awaiting confirmation (reconciliation will settle it).</summary>
        Pending,

        AlreadySuccessful,
        AlreadyReversed,

        /// <summary>Reference already used with different transfer details.</summary>
        ReferenceMismatch,

        /// <summary>Reference was inserted concurrently; no additional debit made.</summary>
        DuplicateReference,

        InvalidRequest,

        /// <summary>Debit was refused; see <see cref="InterbankSendResult.FailureReason"/>.</summary>
        Failed,
        /// <summary>Wrong-PIN lockout is active; see <see cref="InterbankSendResult.RetryAfterSeconds"/>.</summary>
        AccountLocked,


    }

    public sealed record InterbankSendResult(
            InterbankSendOutcome Outcome,
            string Reference,
            string Message,
            TransactionResult? FailureReason = null,
            int? RetryAfterSeconds = null);
}
