using System;
using System.Collections.Generic;
using System.Text;

namespace TatumWelcome2.BLL.DTOs
{
    public class InterbankProtocol
    {
        public const string TransferReceived = "Transfer received successfully.";
        public const string TransferAlreadyProcessed = "Transfer already processed.";
        public const string TransferRejected = "Transfer permanently rejected.";
        public const string TransferPending = "Transfer pending confirmation.";
        public const string RecipientNotFound = "Recipient account not found.";
        public const string ReferenceConflict = "Transfer reference conflict.";
        public const string InvalidRequest = "Invalid transfer request.";
    }
}
