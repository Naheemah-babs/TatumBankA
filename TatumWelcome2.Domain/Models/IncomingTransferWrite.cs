using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.Domain.Models
{
    public enum IncomingTransferWriteStatus
    {
        /// <summary>Recipient credited; record marked Successful.</summary>
        Credited,

        /// <summary>Recipient missing or over the balance cap; record marked Rejected.</summary>
        RecipientRejected,

        /// <summary>Reference already recorded; nothing written.</summary>
        AlreadyExists
    }

    /// <param name="Existing">The stored record when Status is AlreadyExists (null if it vanished).</param>
    public sealed record IncomingTransferWrite(
        IncomingTransferWriteStatus Status,
        IncomingTransferOutcome? Existing = null);
}
