using System;
using System.Collections.Generic;
using System.Text;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.Domain.Interfaces
{
    public interface IInterbankOutgoingTransferRepository
    {
        InterbankOutgoingTransferRecord? GetByReference(string reference);

        /// <summary>
        /// Debits the sender and records a Pending outgoing transfer atomically.
        /// Under the sender's row lock: account exists, reference unused, PIN,
        /// not frozen, sufficient funds. Returns DuplicateReference when the
        /// reference already exists.
        /// </summary>
        TransactionResult DebitAndCreate(
            string senderNumber,
            string recipientNumber,
            string bankUrl,
            decimal amount,
            string pin,
            string reference,
            string description);

        /// <summary>Pending transfers in the given modulo partition, oldest first.</summary>
        List<InterbankOutgoingTransferRecord> GetPendingTransfers(
            WorkPartition partition);

        /// <summary>Pending -> Successful. No effect on any other status.</summary>
        void MarkSuccessful(string reference);

        /// <summary>
        /// Pending -> Reversed, refunding the sender. False when the transfer is
        /// not Pending or the refund would exceed the sender's balance cap.
        /// </summary>
        bool Reverse(string reference, string description);
    }
}
