using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.BLL.DTOs
{
    public enum BillPurchaseOutcome
    {
        /// <summary>Provider approved; the account was debited.</summary>
        Successful,

        /// <summary>Provider declined; the account was not debited.</summary>
        Declined,

        /// <summary>
        /// Refused before reaching the provider (PIN, frozen, funds, account);
        /// see <see cref="BillPurchaseResult.FailureReason"/>. The reference was
        /// not consumed and may be retried.
        /// </summary>
        Rejected,

        /// <summary>Reference already used for a different purchase.</summary>
        ReferenceConflict,

        InvalidRequest,

        /// <summary>Wrong-PIN lockout is active; see <see cref="BillPurchaseResult.RetryAfterSeconds"/>.</summary>
        AccountLocked,
    }
    
    /// <param name="Replayed">
    /// True when this reference was already processed: the stored outcome is
    /// returned and no further debit is made.
    /// </param>
    
    public sealed record BillPurchaseResult(
        BillPurchaseOutcome Outcome,
        string Reference,
        string Message,
        decimal Amount,
        TransactionResult? FailureReason = null,
        bool Replayed = false,
        int? RetryAfterSeconds = null);
}
