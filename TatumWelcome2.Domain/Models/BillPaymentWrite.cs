namespace TatumWelcome2.Domain.Models;

public enum BillPaymentWriteStatus
{
    /// <summary>New reference; the provider decision was recorded.</summary>
    Recorded,

    /// <summary>Reference already recorded; nothing written.</summary>
    AlreadyExists
}

/// <param name="Existing">The stored payment when Status is AlreadyExists (null if it vanished).</param>
public sealed record BillPaymentWrite(
    BillPaymentWriteStatus Status,
    BillPaymentRecord? Existing = null);

public enum CustomerPurchaseWriteStatus
{
    /// <summary>Decision recorded; account debited only if the provider approved.</summary>
    Completed,

    /// <summary>Refused under the account lock; see RejectionReason. Nothing written.</summary>
    Rejected,

    /// <summary>Reference already recorded; nothing written.</summary>
    AlreadyExists
}

public sealed record CustomerPurchaseWrite(
    CustomerPurchaseWriteStatus Status,
    TransactionResult? RejectionReason = null,
    BillPaymentRecord? Existing = null);
